using Shadowbrook.Api.Auth;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Tests.Auth;

public class PermissionsTests
{
    [Fact]
    public void GetPermissions_Admin_ReturnsAllPermissions()
    {
        var permissions = Permissions.GetForRole(AppUserRole.Admin);
        Assert.Contains(Permissions.AppAccess, permissions);
        Assert.Contains(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void GetPermissions_Owner_ReturnsAppAccess()
    {
        var permissions = Permissions.GetForRole(AppUserRole.Owner);
        Assert.Contains(Permissions.AppAccess, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void GetPermissions_Staff_ReturnsAppAccess()
    {
        var permissions = Permissions.GetForRole(AppUserRole.Staff);
        Assert.Contains(Permissions.AppAccess, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }
}
