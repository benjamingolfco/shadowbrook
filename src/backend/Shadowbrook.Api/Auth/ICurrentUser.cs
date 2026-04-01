namespace Shadowbrook.Api.Auth;

public interface ICurrentUser
{
    Guid? AppUserId { get; }
    string? IdentityId { get; }
    Guid? OrganizationId { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
}
