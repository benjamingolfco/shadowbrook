using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Teeforce.Api.Infrastructure.Auth;

namespace Teeforce.Api.Tests.Auth;

public class RequireAppUserHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, RequireAppUserRequirement requirement) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task UserWithAppUserId_Succeeds()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.AppUserId.Returns(Guid.NewGuid());
        var handler = new RequireAppUserHandler(userContext);
        var requirement = new RequireAppUserRequirement();

        var identity = new ClaimsIdentity([], authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task UserWithoutAppUserId_FailsWithRequirementInFailedRequirements()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.AppUserId.Returns((Guid?)null);
        var handler = new RequireAppUserHandler(userContext);
        var requirement = new RequireAppUserRequirement();

        var identity = new ClaimsIdentity([], authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
        Assert.Contains(context.FailureReasons, r => r.Message == "No linked AppUser account");
    }
}
