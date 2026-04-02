using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class RequireAppUserHandler(IUserContext userContext) : AuthorizationHandler<RequireAppUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequireAppUserRequirement requirement)
    {
        if (userContext.AppUserId is not null)
        {
            context.Succeed(requirement);
        }
        else
        {
            // Explicit Fail() is required so that RequireAppUserRequirement appears in
            // AuthorizationFailure.FailedRequirements. Without it, FailedRequirements is empty
            // and AppUserAuthorizationResultHandler cannot detect which requirement failed.
            // These two components are coupled — the result handler uses
            // OfType<RequireAppUserRequirement>() on FailedRequirements to write the
            // { "reason": "no_account" } response.
            context.Fail(new AuthorizationFailureReason(this, "No linked AppUser account"));
        }

        return Task.CompletedTask;
    }
}
