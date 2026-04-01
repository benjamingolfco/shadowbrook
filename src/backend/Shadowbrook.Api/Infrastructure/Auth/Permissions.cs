using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Infrastructure.Auth;

public static class Permissions
{
    public const string AppAccess = "app:access";
    public const string UsersManage = "users:manage";

    private static readonly Dictionary<AppUserRole, string[]> RolePermissions = new()
    {
        [AppUserRole.Admin] = [AppAccess, UsersManage],
        [AppUserRole.Owner] = [AppAccess],
        [AppUserRole.Staff] = [AppAccess],
    };

    public static IReadOnlyList<string> GetForRole(AppUserRole role) =>
        RolePermissions.TryGetValue(role, out var permissions) ? permissions : [];
}
