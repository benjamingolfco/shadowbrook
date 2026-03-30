using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Shadowbrook.Api.Auth;

namespace Shadowbrook.Api.Tests.Auth;

public class PermissionAuthorizationHandlerTests
{
    private static ClaimsPrincipal UserWithPermissions(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim("permission", p)).ToList();
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, PermissionRequirement requirement) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task UserWithMatchingPermission_Succeeds()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(Permissions.AppAccess);
        var user = UserWithPermissions(Permissions.AppAccess);
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task UserWithoutMatchingPermission_DoesNotSucceed()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(Permissions.UsersManage);
        var user = UserWithPermissions(Permissions.AppAccess);
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task UserWithNoPermissionClaims_DoesNotSucceed()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(Permissions.AppAccess);
        var user = UserWithPermissions();
        var context = CreateContext(user, requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
