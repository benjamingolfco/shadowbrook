using Shadowbrook.Domain.AppUserAggregate.Exceptions;
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

    public static AppUser CreateAdmin(string identityId, string email, string displayName)
    {
        return new AppUser
        {
            Id = Guid.CreateVersion7(),
            IdentityId = identityId,
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            Role = AppUserRole.Admin,
            OrganizationId = null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static AppUser CreateOperator(string identityId, string email, string displayName, Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
        }

        return new AppUser
        {
            Id = Guid.CreateVersion7(),
            IdentityId = identityId,
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            Role = AppUserRole.Operator,
            OrganizationId = organizationId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void MakeAdmin()
    {
        Role = AppUserRole.Admin;
        OrganizationId = null;
    }

    public void AssignToOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
        }

        Role = AppUserRole.Operator;
        OrganizationId = organizationId;
    }

    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
