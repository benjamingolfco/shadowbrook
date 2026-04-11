using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.AppUserAggregate;
using Wolverine;

namespace Teeforce.Api.Tests.Auth;

public class AppUserClaimsTransformationTests
{
    private readonly IAppUserClaimsProvider claimsProvider = Substitute.For<IAppUserClaimsProvider>();

    private AppUserClaimsTransformation CreateTransformation(IMessageBus bus) =>
        new(this.claimsProvider, bus, NullLogger<AppUserClaimsTransformation>.Instance);

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(
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
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task UnauthenticatedPrincipal_ReturnsUnchanged()
    {
        var bus = Substitute.For<IMessageBus>();
        var transformation = CreateTransformation(bus);

        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated

        var result = await transformation.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.Null(result.FindFirst("app_user_id"));
        Assert.Null(result.FindFirst("permission"));
    }

    [Fact]
    public async Task AuthenticatedUser_LinkedAppUser_AddsClaims()
    {
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var orgId = Guid.NewGuid();
        var appUserId = Guid.NewGuid();

        this.claimsProvider.GetByIdentityIdAsync(oid)
            .Returns(new AppUserClaimsData(appUserId, orgId, AppUserRole.Operator, IsActive: true, NeedsProfileSetup: false));

        var transformation = CreateTransformation(bus);
        var principal = CreateAuthenticatedPrincipal(oid);

        var result = await transformation.TransformAsync(principal);

        Assert.Equal(appUserId.ToString(), result.FindFirst("app_user_id")?.Value);
        Assert.Equal(orgId.ToString(), result.FindFirst("organization_id")?.Value);
        Assert.Equal("Operator", result.FindFirst("role")?.Value);
        Assert.Contains(result.FindAll("permission"), c => c.Value == Permissions.AppAccess);
    }

    [Fact]
    public async Task AuthenticatedUser_NeedsProfileSetup_DispatchesSetupCommand()
    {
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUserId = Guid.NewGuid();

        this.claimsProvider.GetByIdentityIdAsync(oid)
            .Returns(new AppUserClaimsData(appUserId, null, AppUserRole.Admin, IsActive: true, NeedsProfileSetup: true));

        var transformation = CreateTransformation(bus);
        var principal = CreateAuthenticatedPrincipal(oid, givenName: "Jane", surname: "Smith");

        var result = await transformation.TransformAsync(principal);

        await bus.Received(1).SendAsync(
            Arg.Is<CompleteIdentitySetupCommand>(c =>
                c.AppUserId == appUserId &&
                c.IdentityId == oid &&
                c.FirstName == "Jane" &&
                c.LastName == "Smith"));

        Assert.Equal(appUserId.ToString(), result.FindFirst("app_user_id")?.Value);
    }

    [Fact]
    public async Task AuthenticatedUser_ProfileAlreadySetup_DoesNotDispatchCommand()
    {
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUserId = Guid.NewGuid();

        this.claimsProvider.GetByIdentityIdAsync(oid)
            .Returns(new AppUserClaimsData(appUserId, null, AppUserRole.Admin, IsActive: true, NeedsProfileSetup: false));

        var transformation = CreateTransformation(bus);
        var principal = CreateAuthenticatedPrincipal(oid, givenName: "Jane", surname: "Smith");

        await transformation.TransformAsync(principal);

        await bus.DidNotReceive().SendAsync(Arg.Any<CompleteIdentitySetupCommand>());
    }

    [Fact]
    public async Task AuthenticatedUser_NoAppUser_ReturnsWithoutAppUserClaim()
    {
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();

        this.claimsProvider.GetByIdentityIdAsync(oid).Returns((AppUserClaimsData?)null);

        var transformation = CreateTransformation(bus);
        var principal = CreateAuthenticatedPrincipal(oid, email: "unknown@example.com");

        var result = await transformation.TransformAsync(principal);

        // No AppUser found — principal returned without app_user_id claim.
        // The authorization layer (RequireAppUserHandler) is responsible for the 403 rejection.
        Assert.Null(result.FindFirst("app_user_id"));
    }

    [Fact]
    public async Task InactiveUser_EnrichedWithoutPermissions()
    {
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUserId = Guid.NewGuid();

        this.claimsProvider.GetByIdentityIdAsync(oid)
            .Returns(new AppUserClaimsData(appUserId, null, AppUserRole.Admin, IsActive: false, NeedsProfileSetup: false));

        var transformation = CreateTransformation(bus);
        var principal = CreateAuthenticatedPrincipal(oid);

        var result = await transformation.TransformAsync(principal);

        Assert.NotNull(result.FindFirst("app_user_id"));
        Assert.NotNull(result.FindFirst("role"));
        Assert.Null(result.FindFirst("permission"));
    }

    [Fact]
    public async Task TransformAsync_CalledTwice_DoesNotDuplicateClaims()
    {
        var bus = Substitute.For<IMessageBus>();

        var oid = Guid.NewGuid().ToString();
        var appUserId = Guid.NewGuid();

        this.claimsProvider.GetByIdentityIdAsync(oid)
            .Returns(new AppUserClaimsData(appUserId, null, AppUserRole.Admin, IsActive: true, NeedsProfileSetup: false));

        var transformation = CreateTransformation(bus);
        var principal = CreateAuthenticatedPrincipal(oid);

        // First call enriches the principal
        var result = await transformation.TransformAsync(principal);
        // Second call should be a no-op due to the duplicate claims guard
        var result2 = await transformation.TransformAsync(result);

        var appUserIdClaims = result2.FindAll("app_user_id").ToList();
        Assert.Single(appUserIdClaims);
    }
}
