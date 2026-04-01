using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.OrganizationAggregate;

namespace Shadowbrook.Api.Tests.Auth;

public class AppUserEnrichmentMiddlewareTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IMemoryCache CreateMemoryCache() =>
        new MemoryCache(Options.Create(new MemoryCacheOptions()));

    private static IHostEnvironment CreateHostEnvironment(string environmentName = "Development")
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return env;
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string oid, string? email = null, string? name = null)
    {
        var claims = new List<Claim> { new("oid", oid) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);
        return new DefaultHttpContext { User = principal };
    }

    [Fact]
    public async Task UnauthenticatedRequest_PassesThroughWithoutEnrichment()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var nextCalled = false;
        var middleware = new AppUserEnrichmentMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.Null(context.User.FindFirst("app_user_id"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task AuthenticatedUser_ExistingAppUser_GetsEnrichedWithClaims()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var org = Organization.Create("Acme Golf");
        db.Organizations.Add(org);
        var course = Course.Create(org.Id, "Acme Course", "America/New_York");
        db.Courses.Add(course);
        var appUser = AppUser.Create(oid, "op@example.com", "Op User", AppUserRole.Owner, org.Id);
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        Assert.Equal(appUser.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal(org.Id.ToString(), context.User.FindFirst("organization_id")?.Value);
        Assert.Equal("Owner", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
        Assert.Contains(context.User.FindAll("course_id"), c => c.Value == course.Id.ToString());
    }

    [Fact]
    public async Task OwnerUser_GetsOrgCourseIdsEnrichedAsClaims()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var org = Organization.Create("Pine Valley Golf");
        db.Organizations.Add(org);
        var course1 = Course.Create(org.Id, "Course One", "America/New_York");
        var course2 = Course.Create(org.Id, "Course Two", "America/New_York");
        db.Courses.AddRange(course1, course2);
        var appUser = AppUser.Create(oid, "owner@example.com", "Owner User", AppUserRole.Owner, org.Id);
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        var enrichedCourseIds = context.User.FindAll("course_id").Select(c => c.Value).ToHashSet();
        Assert.Contains(course1.Id.ToString(), enrichedCourseIds);
        Assert.Contains(course2.Id.ToString(), enrichedCourseIds);
        Assert.Null(context.User.FindFirst("course_access"));
    }

    [Fact]
    public async Task AdminUser_GetsUniversalAccessClaimButNoCourseIds()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var org = Organization.Create("Acme Golf");
        db.Organizations.Add(org);
        var course = Course.Create(org.Id, "Acme Course", "America/New_York");
        db.Courses.Add(course);
        var appUser = AppUser.Create(oid, "admin@example.com", "Admin User", AppUserRole.Admin, null);
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        Assert.Equal("all", context.User.FindFirst("course_access")?.Value);
        Assert.Empty(context.User.FindAll("course_id"));
    }

    [Fact]
    public async Task OwnerUser_WithNoOrg_GetsNoCourseIdClaims()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.Create(oid, "owner@example.com", "Owner User", AppUserRole.Owner, organizationId: null);
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        Assert.Empty(context.User.FindAll("course_id"));
        Assert.Null(context.User.FindFirst("course_access"));
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUserRow_AutoProvisionedAsStaff()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "new@example.com", name: "New User");

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        var created = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == oid);
        Assert.NotNull(created);
        Assert.Equal(AppUserRole.Staff, created!.Role);
        Assert.Null(created.OrganizationId);
        Assert.True(created.IsActive);

        Assert.Equal(created.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal("Staff", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task InactiveUser_IsNotEnriched()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.Create(oid, "inactive@example.com", "Inactive User", AppUserRole.Staff, null);
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var nextCalled = false;
        var middleware = new AppUserEnrichmentMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateAuthenticatedContext(oid);
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(context.User.FindFirst("app_user_id"));
        Assert.Null(context.User.FindFirst("permission"));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Your account has been deactivated.", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InactiveUser_CachedStatus_Returns403WithoutDbHit()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.Create(oid, "inactive@example.com", "Inactive User", AppUserRole.Staff, null);
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);

        // First request — populates cache with null (inactive status)
        var context1 = CreateAuthenticatedContext(oid);
        context1.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context1, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);
        Assert.Equal(StatusCodes.Status403Forbidden, context1.Response.StatusCode);

        // Remove the user from the DB to prove the second request doesn't hit it
        db.AppUsers.Remove(appUser);
        await db.SaveChangesAsync();

        // Second request — should return 403 from cache, not from DB
        var context2 = CreateAuthenticatedContext(oid);
        context2.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context2, db, cache, new ConfigurationBuilder().Build(), CreateHostEnvironment(), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, context2.Response.StatusCode);

        context2.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonDocument.ParseAsync(context2.Response.Body);
        Assert.Equal("Your account has been deactivated.", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SeedAdminEmail_InDevelopment_AutoPromotesToAdmin()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "dev-admin@example.com", name: "Dev Admin");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SeedAdminEmails"] = "dev-admin@example.com",
            })
            .Build();

        await middleware.InvokeAsync(context, db, cache, config, CreateHostEnvironment("Development"), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        var created = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == oid);
        Assert.NotNull(created);
        Assert.Equal(AppUserRole.Admin, created!.Role);
        Assert.Equal("Admin", context.User.FindFirst("role")?.Value);
    }

    [Fact]
    public async Task SeedAdminEmail_InProduction_AutoProvisionedAsStaff()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "dev-admin@example.com", name: "Dev Admin");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SeedAdminEmails"] = "dev-admin@example.com",
            })
            .Build();

        await middleware.InvokeAsync(context, db, cache, config, CreateHostEnvironment("Production"), NullLogger<AppUserEnrichmentMiddleware>.Instance);

        var created = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == oid);
        Assert.NotNull(created);
        Assert.Equal(AppUserRole.Staff, created!.Role);
        Assert.Equal("Staff", context.User.FindFirst("role")?.Value);
    }
}
