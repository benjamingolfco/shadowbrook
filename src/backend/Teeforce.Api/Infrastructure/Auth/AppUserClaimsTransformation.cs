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
            var email = ExtractEmail(principal);

            if (!string.IsNullOrEmpty(email))
            {
                data = await claimsProvider.GetByEmailUnlinkedAsync(email);

                if (data is not null)
                {
                    var firstName = principal.FindFirst("given_name")?.Value ?? string.Empty;
                    var lastName = principal.FindFirst("family_name")?.Value ?? string.Empty;

                    // Fire-and-forget: CompleteIdentitySetupCommand runs asynchronously via
                    // Wolverine's outbox. We already have the AppUserClaimsData from
                    // GetByEmailUnlinkedAsync, so enrichment proceeds immediately without
                    // waiting for the handler to complete.
                    await bus.SendAsync(new CompleteIdentitySetupCommand(data.AppUserId, oid, firstName, lastName));
                }
            }
        }

        if (data is null)
        {
            // No AppUser found by oid or email match. Return the principal unchanged.
            // The authorization layer (RequireAppUserHandler) handles rejection.
            logger.LogWarning("No AppUser found for oid {Oid}", oid);
            return principal;
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

    private static string ExtractEmail(ClaimsPrincipal principal) =>
        principal.FindFirst("emails")?.Value
        ?? principal.FindFirst("email")?.Value
        ?? principal.FindFirst(ClaimTypes.Email)?.Value
        ?? principal.FindFirst(ClaimTypes.Name)?.Value
        ?? principal.FindFirst("upn")?.Value
        ?? string.Empty;
}
