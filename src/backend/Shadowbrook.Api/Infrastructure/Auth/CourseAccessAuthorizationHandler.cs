using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class CourseAccessAuthorizationHandler : AuthorizationHandler<CourseAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseAccessRequirement requirement)
    {
        if (!context.User.FindAll("permission").Any(c => c.Value == Permissions.AppAccess))
        {
            return Task.CompletedTask;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return Task.CompletedTask;
        }

        var courseIdStr = httpContext.Request.RouteValues["courseId"]?.ToString();
        if (!Guid.TryParse(courseIdStr, out var courseId))
        {
            return Task.CompletedTask;
        }

        var hasUniversalAccess = context.User.FindAll("course_access").Any(c => c.Value == "all");
        if (hasUniversalAccess)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var courseIds = context.User.FindAll("course_id")
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        if (courseIds.Contains(courseId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
