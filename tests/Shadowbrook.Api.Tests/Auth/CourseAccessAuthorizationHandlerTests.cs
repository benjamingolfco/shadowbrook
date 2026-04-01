using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Tests.Auth;

public class CourseAccessAuthorizationHandlerTests
{
    private static HttpContext CreateHttpContextWithCourseId(Guid courseId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["courseId"] = courseId.ToString(),
        };
        return httpContext;
    }

    private static ClaimsPrincipal BuildUser(
        bool hasUniversalAccess = false,
        IEnumerable<Guid>? courseIds = null,
        bool hasAppAccess = true)
    {
        var claims = new List<Claim>();

        if (hasAppAccess)
        {
            claims.Add(new Claim("permission", Permissions.AppAccess));
        }

        if (hasUniversalAccess)
        {
            claims.Add(new Claim("course_access", "all"));
        }

        foreach (var id in courseIds ?? [])
        {
            claims.Add(new Claim("course_id", id.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, HttpContext httpContext) =>
        new([new CourseAccessRequirement()], user, resource: httpContext);

    [Fact]
    public async Task Admin_UniversalAccess_AlwaysSucceeds()
    {
        var handler = new CourseAccessAuthorizationHandler();

        var courseId = Guid.NewGuid();
        var user = BuildUser(hasUniversalAccess: true);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Owner_CourseIdInClaims_Succeeds()
    {
        var handler = new CourseAccessAuthorizationHandler();

        var courseId = Guid.NewGuid();
        var user = BuildUser(courseIds: [courseId]);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Owner_CourseIdNotInClaims_DoesNotSucceed()
    {
        var handler = new CourseAccessAuthorizationHandler();

        var courseId = Guid.NewGuid();
        var otherCourseId = Guid.NewGuid();
        var user = BuildUser(courseIds: [otherCourseId]);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Staff_AssignedToCourse_Succeeds()
    {
        var handler = new CourseAccessAuthorizationHandler();

        var courseId = Guid.NewGuid();
        var user = BuildUser(courseIds: [courseId]);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Staff_NotAssignedToCourse_DoesNotSucceed()
    {
        var handler = new CourseAccessAuthorizationHandler();

        var courseId = Guid.NewGuid();
        var otherCourseId = Guid.NewGuid();
        var user = BuildUser(courseIds: [otherCourseId]);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task NoAppAccessPermission_DoesNotSucceed()
    {
        var handler = new CourseAccessAuthorizationHandler();

        var courseId = Guid.NewGuid();
        var user = BuildUser(hasUniversalAccess: true, hasAppAccess: false);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
