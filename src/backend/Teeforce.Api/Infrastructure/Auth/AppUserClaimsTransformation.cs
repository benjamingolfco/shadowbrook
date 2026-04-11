using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Domain.AppUserAggregate;
using Wolverine;

namespace Teeforce.Api.Infrastructure.Auth;

// NOTE: This queries the DB on every authenticated request. If this becomes a performance
// concern, reintroduce IMemoryCache (single instance) or IDistributedCache (multiple replicas).
public class AppUserClaimsTransformation(
    IAppUserClaimsProvider claimsProvider,
    IMessageBus bus,
    ILogger<AppUserClaimsTransformation> logger) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var oid = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (oid is null)
        {
            return principal;
        }

        // Duplicate claims guard: IClaimsTransformation.TransformAsync can be called multiple
        // times per request (e.g., when HttpContext.AuthenticateAsync() is invoked more than once).
        // Check for an existing app_user_id claim and return early if already enriched.
        if (principal.HasClaim(c => c.Type == "app_user_id"))
        {
            return principal;
        }

        var data = await claimsProvider.GetByIdentityIdAsync(oid);

        if (data is null)
        {
            // No AppUser found by oid. Return the principal unchanged.
            // The authorization layer (RequireAppUserHandler) handles rejection.
            logger.LogWarning("No AppUser found for oid {Oid}", oid);
            return principal;
        }

        if (data.NeedsProfileSetup)
        {
            var firstName = principal.FindFirst("given_name")?.Value;
            var lastName = principal.FindFirst("family_name")?.Value;

            if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
            {
                // Fire-and-forget: populate first/last name from the identity token.
                // This runs on the user's first login after being invited.
                await bus.SendAsync(new CompleteIdentitySetupCommand(
                    data.AppUserId, firstName ?? string.Empty, lastName ?? string.Empty));
            }
        }

        var permissions = data.IsActive
            ? Permissions.GetForRole(data.Role)
            : [];

        var claimsList = new List<Claim>
        {
            new("app_user_id", data.AppUserId.ToString()),
        };

        if (data.OrganizationId.HasValue)
        {
            claimsList.Add(new Claim("organization_id", data.OrganizationId.Value.ToString()));
        }

        claimsList.Add(new Claim("role", data.Role.ToString()));

        foreach (var permission in permissions)
        {
            claimsList.Add(new Claim("permission", permission));
        }

        principal.AddIdentity(new ClaimsIdentity(claimsList));
        return principal;
    }

}
