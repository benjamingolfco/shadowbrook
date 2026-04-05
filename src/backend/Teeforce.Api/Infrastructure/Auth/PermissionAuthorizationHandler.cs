using Microsoft.AspNetCore.Authorization;

namespace Teeforce.Api.Infrastructure.Auth;

public class PermissionAuthorizationHandler(IUserContext userContext) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (userContext.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
