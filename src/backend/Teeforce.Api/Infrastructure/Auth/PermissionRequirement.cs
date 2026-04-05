using Microsoft.AspNetCore.Authorization;

namespace Teeforce.Api.Infrastructure.Auth;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
