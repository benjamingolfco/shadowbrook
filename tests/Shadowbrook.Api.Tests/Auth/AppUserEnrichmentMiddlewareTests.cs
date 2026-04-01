using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

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

        await middleware.InvokeAsync(context, db, cache, Options.Create(new AuthSettings()));

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
        var org = Shadowbrook.Domain.OrganizationAggregate.Organization.Create("Acme Golf");
        db.Organizations.Add(org);
        var appUser = AppUser.CreateOperator(oid, "op@example.com", "Op User", org.Id);
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, Options.Create(new AuthSettings()));

        Assert.Equal(appUser.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal(org.Id.ToString(), context.User.FindFirst("organization_id")?.Value);
        Assert.Equal("Operator", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUserRow_SeedAdminEmail_AutoProvisionedAsAdmin()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "seed-admin@shadowbrook.com", name: "Seed Admin");
        var authSettings = new AuthSettings { SeedAdminEmails = "seed-admin@shadowbrook.com" };

        await middleware.InvokeAsync(context, db, cache, Options.Create(authSettings));

        var created = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == oid);
        Assert.NotNull(created);
        Assert.Equal(AppUserRole.Admin, created!.Role);
        Assert.Null(created.OrganizationId);
        Assert.True(created.IsActive);

        Assert.Equal(created.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal("Admin", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUserRow_NonSeedEmail_SkipsEnrichment()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var nextCalled = false;
        var middleware = new AppUserEnrichmentMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext(oid, email: "unknown@example.com", name: "Unknown User");

        await middleware.InvokeAsync(context, db, cache, Options.Create(new AuthSettings()));

        Assert.True(nextCalled);
        var created = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == oid);
        Assert.Null(created);
        Assert.Null(context.User.FindFirst("app_user_id"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task InactiveUser_IsEnrichedWithoutPermissions()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.CreateAdmin(oid, "inactive@example.com", "Inactive User");
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

        await middleware.InvokeAsync(context, db, cache, Options.Create(new AuthSettings()));

        Assert.True(nextCalled);
        Assert.NotNull(context.User.FindFirst("app_user_id"));
        Assert.NotNull(context.User.FindFirst("role"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task InactiveUser_DoesNotRecordLogin()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.CreateAdmin(oid, "inactive@example.com", "Inactive User");
        appUser.RecordLogin();
        var loginBefore = appUser.LastLoginAt;
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, Options.Create(new AuthSettings()));

        Assert.Equal(loginBefore, appUser.LastLoginAt);
    }
}
