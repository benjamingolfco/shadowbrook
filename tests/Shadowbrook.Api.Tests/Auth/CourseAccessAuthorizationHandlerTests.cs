using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.OrganizationAggregate;

namespace Shadowbrook.Api.Tests.Auth;

public class CourseAccessAuthorizationHandlerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

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
        AppUserRole role,
        Guid? organizationId = null,
        IEnumerable<Guid>? courseIds = null,
        bool hasAppAccess = true)
    {
        var claims = new List<Claim>
        {
            new("role", role.ToString()),
        };

        if (hasAppAccess)
        {
            claims.Add(new Claim("permission", Permissions.AppAccess));
        }

        if (organizationId.HasValue)
        {
            claims.Add(new Claim("organization_id", organizationId.Value.ToString()));
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
    public async Task Admin_AlwaysSucceeds()
    {
        await using var db = CreateInMemoryDbContext();
        var handler = new CourseAccessAuthorizationHandler(db);

        var courseId = Guid.NewGuid();
        var user = BuildUser(AppUserRole.Admin);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Owner_CourseInTheirOrg_Succeeds()
    {
        await using var db = CreateInMemoryDbContext();

        var org = Organization.Create("Pine Valley Golf");
        db.Organizations.Add(org);
        var course = Course.Create(org.Id, "Pine Valley", "America/New_York");
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var handler = new CourseAccessAuthorizationHandler(db);
        var user = BuildUser(AppUserRole.Owner, organizationId: org.Id);
        var httpContext = CreateHttpContextWithCourseId(course.Id);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Owner_CourseInDifferentOrg_DoesNotSucceed()
    {
        await using var db = CreateInMemoryDbContext();

        var org = Organization.Create("Pine Valley Golf");
        db.Organizations.Add(org);
        var course = Course.Create(org.Id, "Pine Valley", "America/New_York");
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var handler = new CourseAccessAuthorizationHandler(db);
        var differentOrgId = Guid.NewGuid();
        var user = BuildUser(AppUserRole.Owner, organizationId: differentOrgId);
        var httpContext = CreateHttpContextWithCourseId(course.Id);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Staff_AssignedToCourse_Succeeds()
    {
        await using var db = CreateInMemoryDbContext();
        var handler = new CourseAccessAuthorizationHandler(db);

        var courseId = Guid.NewGuid();
        var user = BuildUser(AppUserRole.Staff, courseIds: [courseId]);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Staff_NotAssignedToCourse_DoesNotSucceed()
    {
        await using var db = CreateInMemoryDbContext();
        var handler = new CourseAccessAuthorizationHandler(db);

        var courseId = Guid.NewGuid();
        var otherCourseId = Guid.NewGuid();
        var user = BuildUser(AppUserRole.Staff, courseIds: [otherCourseId]);
        var httpContext = CreateHttpContextWithCourseId(courseId);
        var context = CreateContext(user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
