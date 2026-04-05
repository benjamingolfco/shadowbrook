namespace Teeforce.Api.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    public const string RequireAppUser = nameof(RequireAppUser);
    public const string RequireAppAccess = nameof(RequireAppAccess);
    public const string RequireUsersManage = nameof(RequireUsersManage);
}
