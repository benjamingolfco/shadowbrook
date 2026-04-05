using Teeforce.Domain.AppUserAggregate;

namespace Teeforce.Api.Infrastructure.Auth;

public static class Permissions
{
    public const string AppAccess = "app:access";
    public const string UsersManage = "users:manage";

    private static readonly Dictionary<AppUserRole, string[]> RolePermissions = new()
    {
        [AppUserRole.Admin] = [AppAccess, UsersManage],
        [AppUserRole.Operator] = [AppAccess],
    };

    public static IReadOnlyList<string> GetForRole(AppUserRole role) =>
        RolePermissions.TryGetValue(role, out var permissions) ? permissions : [];
}
