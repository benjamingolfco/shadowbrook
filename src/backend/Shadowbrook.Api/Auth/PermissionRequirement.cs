using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Auth;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
