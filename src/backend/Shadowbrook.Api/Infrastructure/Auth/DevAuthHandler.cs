using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using Shadowbrook.Api.Infrastructure;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment() && !environment.IsIntegrationTesting())
        {
            throw new InvalidOperationException("DevAuth is only available in Development and Testing environments.");
        }

        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var devIdentityId = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(devIdentityId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Only set the oid claim — all application claims (app_user_id, organization_id, role,
        // permission) are added by IClaimsTransformation (AppUserClaimsTransformation) which runs
        // after authentication. This mirrors the production Entra ID path exactly.
        var claims = new List<Claim> { new("oid", devIdentityId) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
