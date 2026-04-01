using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shadowbrook.Api.Infrastructure.Data;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceProvider serviceProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var devIdentityId = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(devIdentityId))
        {
            return AuthenticateResult.NoResult();
        }

        var claims = new List<Claim> { new("oid", devIdentityId) };

        // Look up the user in the database to add app_user_id, organization_id, and permission claims
        using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == devIdentityId);

        if (user is not null)
        {
            claims.Add(new Claim("app_user_id", user.Id.ToString()));

            if (user.OrganizationId.HasValue)
            {
                claims.Add(new Claim("organization_id", user.OrganizationId.Value.ToString()));
            }

            // Add permission claims based on role
            foreach (var permission in Permissions.GetForRole(user.Role))
            {
                claims.Add(new Claim("permission", permission));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
