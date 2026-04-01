using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
