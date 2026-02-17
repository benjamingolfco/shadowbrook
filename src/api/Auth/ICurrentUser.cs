namespace Shadowbrook.Api.Auth;

public interface ICurrentUser
{
    Guid? TenantId { get; }
    bool HasTenantId { get; }
}
