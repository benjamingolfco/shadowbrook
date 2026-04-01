using Microsoft.AspNetCore.Authorization;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class ScopeRequirement(params string[] acceptedScopes) : IAuthorizationRequirement
{
    public string[] AcceptedScopes { get; } = acceptedScopes;
}
