using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Auth;

public class CourseAccessAuthorizationHandler(ApplicationDbContext db)
    : AuthorizationHandler<CourseAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseAccessRequirement requirement)
    {
        if (!context.User.FindAll("permission").Any(c => c.Value == Permissions.AppAccess))
        {
            return;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        var courseIdStr = httpContext.Request.RouteValues["courseId"]?.ToString();
        if (!Guid.TryParse(courseIdStr, out var courseId))
        {
            return;
        }

        var roleClaim = context.User.FindFirst("role")?.Value;
        if (!Enum.TryParse<AppUserRole>(roleClaim, out var role))
        {
            return;
        }

        switch (role)
        {
            case AppUserRole.Admin:
                context.Succeed(requirement);
                return;

            case AppUserRole.Owner:
                var orgIdClaim = context.User.FindFirst("organization_id")?.Value;
                if (orgIdClaim is not null && Guid.TryParse(orgIdClaim, out var orgId))
                {
                    var courseInOrg = await db.Courses
                        .IgnoreQueryFilters()
                        .AnyAsync(c => c.Id == courseId && c.OrganizationId == orgId);
                    if (courseInOrg)
                    {
                        context.Succeed(requirement);
                    }
                }

                return;

            case AppUserRole.Staff:
                var courseIds = context.User.FindAll("course_id")
                    .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToHashSet();
                if (courseIds.Contains(courseId))
                {
                    context.Succeed(requirement);
                }

                return;
        }
    }
}
