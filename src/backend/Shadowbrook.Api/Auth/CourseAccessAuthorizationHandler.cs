using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Auth;

public class CourseAccessAuthorizationHandler : AuthorizationHandler<CourseAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CourseAccessRequirement requirement) =>
        // Stub — full implementation in Task 7
        Task.CompletedTask;
}
