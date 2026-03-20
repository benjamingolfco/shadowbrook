namespace Shadowbrook.Api.Auth;

public interface ICurrentUser
{
    string? UserId { get; }
    Guid? TenantId { get; }
    bool HasTenantId { get; }
}
