using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Tests.Auth;

public class ScopeAuthorizationHandlerTests
{
    private static ClaimsPrincipal UserWithScopeClaim(string claimType, string scopeValue)
    {
        var identity = new ClaimsIdentity(
            [new Claim(claimType, scopeValue)],
            authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal UserWithNoScopeClaim()
    {
        var identity = new ClaimsIdentity(authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, ScopeRequirement requirement) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task TokenWithMatchingScope_Succeeds()
    {
        var handler = new ScopeAuthorizationHandler();
        var requirement = new ScopeRequirement("access_as_user");
        var user = UserWithScopeClaim("scp", "access_as_user");
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task TokenWithMultipleScopesOneMatching_Succeeds()
    {
        var handler = new ScopeAuthorizationHandler();
        var requirement = new ScopeRequirement("access_as_user");
        var user = UserWithScopeClaim("scp", "openid profile access_as_user");
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task TokenWithNonMatchingScope_DoesNotSucceed()
    {
        var handler = new ScopeAuthorizationHandler();
        var requirement = new ScopeRequirement("access_as_user");
        var user = UserWithScopeClaim("scp", "openid profile");
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task TokenWithNoScopeClaim_DoesNotSucceed()
    {
        var handler = new ScopeAuthorizationHandler();
        var requirement = new ScopeRequirement("access_as_user");
        var user = UserWithNoScopeClaim();
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task TokenWithV1ScopeClaimFormat_Succeeds()
    {
        var handler = new ScopeAuthorizationHandler();
        var requirement = new ScopeRequirement("access_as_user");
        var user = UserWithScopeClaim(
            "http://schemas.microsoft.com/identity/claims/scope",
            "access_as_user");
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task EmptyAcceptedScopes_DoesNotSucceed()
    {
        var handler = new ScopeAuthorizationHandler();
        var requirement = new ScopeRequirement();
        var user = UserWithScopeClaim("scp", "access_as_user");
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
