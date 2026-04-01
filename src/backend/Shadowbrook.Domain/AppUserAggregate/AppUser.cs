using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate;

public class AppUser : Entity
{
    public string IdentityId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public AppUserRole Role { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private AppUser() { } // EF

    public static AppUser Create(
        string identityId, string email, string displayName,
        AppUserRole role, Guid? organizationId)
    {
        return new AppUser
        {
            Id = Guid.CreateVersion7(),
            IdentityId = identityId,
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            Role = role,
            OrganizationId = organizationId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateRole(AppUserRole role, Guid? organizationId)
    {
        Role = role;
        OrganizationId = organizationId;
    }

    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
