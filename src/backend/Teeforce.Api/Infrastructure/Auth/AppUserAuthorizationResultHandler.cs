using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Wolverine.Attributes;

namespace Teeforce.Api.Infrastructure.Auth;

[WolverineIgnore]
public class AppUserAuthorizationResultHandler(IAuthorizationMiddlewareResultHandler defaultHandler)
    : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        // Write a specific 403 with { "reason": "no_account" } when all four conditions are true:
        //   1. The result is forbidden (not just unauthorized)
        //   2. The user IS authenticated (has a valid token but no AppUser record)
        //   3. The user does NOT have an app_user_id claim (claims transformation found no AppUser)
        //   4. RequireAppUserRequirement is the specific requirement that failed
        //
        // This detection depends on RequireAppUserHandler calling context.Fail() explicitly.
        // Without that call, FailedRequirements is empty and condition 4 is never true.
        var isAppUserFailure =
            authorizeResult.Forbidden &&
            context.User.Identity?.IsAuthenticated == true &&
            !context.User.HasClaim(c => c.Type == "app_user_id") &&
            authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<RequireAppUserRequirement>().Any() == true;

        if (isAppUserFailure)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { reason = "no_account" });
            return;
        }

        await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
