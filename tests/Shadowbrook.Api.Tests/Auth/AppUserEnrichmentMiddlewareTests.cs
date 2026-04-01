using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shadowbrook.Api.Features.Auth.Handlers;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Wolverine;

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

    private static AppUserEnrichmentMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLoggerFactory.Instance);

    private static DefaultHttpContext CreateAuthenticatedContext(
        string oid, string? email = null, string? givenName = null, string? surname = null)
    {
        var claims = new List<Claim> { new("oid", oid) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (givenName is not null)
        {
            claims.Add(new Claim("given_name", givenName));
        }

        if (surname is not null)
        {
            claims.Add(new Claim("family_name", surname));
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
        var bus = Substitute.For<IMessageBus>();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.True(nextCalled);
        Assert.Null(context.User.FindFirst("app_user_id"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task AuthenticatedUser_ExistingLinkedAppUser_GetsEnrichedWithClaims()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var org = Shadowbrook.Domain.OrganizationAggregate.Organization.Create("Acme Golf");
        db.Organizations.Add(org);
        var appUser = AppUser.CreateOperator("op@example.com", org.Id);
        appUser.CompleteIdentitySetup(oid, "Op", "User");
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.Equal(appUser.Id.ToString(), context.User.FindFirst("app_user_id")?.Value);
        Assert.Equal(org.Id.ToString(), context.User.FindFirst("organization_id")?.Value);
        Assert.Equal("Operator", context.User.FindFirst("role")?.Value);
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task AuthenticatedUser_UnlinkedAppUser_MatchesByEmail_DispatchesSetupCommand()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var appUser = AppUser.CreateAdmin("admin@example.com");
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var oid = Guid.NewGuid().ToString();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid, email: "admin@example.com", givenName: "Jane", surname: "Smith");

        await middleware.InvokeAsync(context, db, cache, bus);

        await bus.Received(1).InvokeAsync(
            Arg.Is<CompleteIdentitySetupCommand>(c =>
                c.AppUserId == appUser.Id &&
                c.IdentityId == oid &&
                c.FirstName == "Jane" &&
                c.LastName == "Smith"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUser_Returns403WithNoAccountReason()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext(oid, email: "unknown@example.com");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task InactiveLinkedUser_IsEnrichedWithoutPermissions()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.CreateAdmin("inactive@example.com");
        appUser.CompleteIdentitySetup(oid, "Inactive", "User");
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.True(nextCalled);
        Assert.NotNull(context.User.FindFirst("app_user_id"));
        Assert.NotNull(context.User.FindFirst("role"));
        Assert.Null(context.User.FindFirst("permission"));
    }

    [Fact]
    public async Task InactiveLinkedUser_DoesNotRecordLogin()
    {
        await using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUser = AppUser.CreateAdmin("inactive@example.com");
        appUser.CompleteIdentitySetup(oid, "Inactive", "User");
        appUser.RecordLogin();
        var loginBefore = appUser.LastLoginAt;
        appUser.Deactivate();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext(oid);

        await middleware.InvokeAsync(context, db, cache, bus);

        Assert.Equal(loginBefore, appUser.LastLoginAt);
    }
}
