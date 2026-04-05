using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Teeforce.Api.Infrastructure.Auth;

namespace Teeforce.Api.Tests.Auth;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, PermissionRequirement requirement) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task UserWithMatchingPermission_Succeeds()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.HasPermission(Permissions.AppAccess).Returns(true);
        var handler = new PermissionAuthorizationHandler(userContext);
        var requirement = new PermissionRequirement(Permissions.AppAccess);
        var user = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "test"));
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task UserWithoutMatchingPermission_DoesNotSucceed()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.HasPermission(Permissions.UsersManage).Returns(false);
        var handler = new PermissionAuthorizationHandler(userContext);
        var requirement = new PermissionRequirement(Permissions.UsersManage);
        var user = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "test"));
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task UserWithNoPermissions_DoesNotSucceed()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.HasPermission(Arg.Any<string>()).Returns(false);
        var handler = new PermissionAuthorizationHandler(userContext);
        var requirement = new PermissionRequirement(Permissions.AppAccess);
        var user = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "test"));
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
