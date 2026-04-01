using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    // v2.0 tokens use "scp"; v1.0 tokens use the full URI form
    private const string ScopeClaimType = "scp";
    private const string ScopeClaimTypeV1 = "http://schemas.microsoft.com/identity/claims/scope";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (requirement.AcceptedScopes.Length == 0)
        {
            return Task.CompletedTask;
        }

        var scopeClaim = context.User.FindFirst(ScopeClaimType)
                      ?? context.User.FindFirst(ScopeClaimTypeV1);

        if (scopeClaim is not null)
        {
            var tokenScopes = scopeClaim.Value.Split(' ');
            if (requirement.AcceptedScopes.Any(s => tokenScopes.Contains(s)))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
