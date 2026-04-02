using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Tests.Auth;

public class RequireAppUserHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, RequireAppUserRequirement requirement) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task UserWithAppUserIdClaim_Succeeds()
    {
        var handler = new RequireAppUserHandler();
        var requirement = new RequireAppUserRequirement();

        var claims = new List<Claim> { new("app_user_id", Guid.NewGuid().ToString()) };
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task UserWithoutAppUserIdClaim_FailsWithRequirementInFailedRequirements()
    {
        var handler = new RequireAppUserHandler();
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
