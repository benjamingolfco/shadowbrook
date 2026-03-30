using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shadowbrook.Api.Auth;
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

        await middleware.InvokeAsync(context, db, cache);

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
        var appUser = AppUser.Create(oid, "op@example.com", "Op User", AppUserRole.Owner, org.Id);
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache);

        Assert.Equal(appUser.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal(org.Id.ToString(), context.User.FindFirst("organization_id")?.Value);
        Assert.Equal("Owner", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUserRow_AutoProvisionedAsStaff()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();

        var oid = Guid.NewGuid().ToString();
        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "new@example.com", name: "New User");

        await middleware.InvokeAsync(context, db, cache);

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

        var middleware = new AppUserEnrichmentMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache);

        Assert.Null(context.User.FindFirst("app_user_id"));
        Assert.Null(context.User.FindFirst("permission"));
    }
}
