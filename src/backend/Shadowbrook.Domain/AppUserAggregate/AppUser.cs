using Shadowbrook.Domain.AppUserAggregate.Events;
using Shadowbrook.Domain.AppUserAggregate.Exceptions;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate;

public class AppUser : Entity
{
    public string? IdentityId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public AppUserRole Role { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private AppUser() { } // EF

    public static AppUser CreateAdmin(string email)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim(),
            Role = AppUserRole.Admin,
            OrganizationId = null,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        user.AddDomainEvent(new AppUserCreated
        {
            AppUserId = user.Id,
            Email = user.Email,
            Role = user.Role,
        });

        return user;
    }

    public static AppUser CreateOperator(string email, Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim(),
            Role = AppUserRole.Operator,
            OrganizationId = organizationId,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        user.AddDomainEvent(new AppUserCreated
        {
            AppUserId = user.Id,
            Email = user.Email,
            Role = user.Role,
        });

        return user;
    }

    public void CompleteIdentitySetup(string identityId, string firstName, string lastName)
    {
        if (this.IdentityId is not null && this.IdentityId == identityId)
        {
            return; // Idempotent — already linked to this identity
        }

        if (this.IdentityId is not null)
        {
            throw new IdentityAlreadyLinkedException();
        }

        this.IdentityId = identityId;
        this.FirstName = firstName;
        this.LastName = lastName;
        this.IsActive = true;

        AddDomainEvent(new AppUserSetupCompleted
        {
            AppUserId = this.Id,
            Email = this.Email,
        });
    }

    public void MakeAdmin()
    {
        this.Role = AppUserRole.Admin;
        this.OrganizationId = null;
    }

    public void AssignToOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
        }

        this.Role = AppUserRole.Operator;
        this.OrganizationId = organizationId;
    }

    public void RecordLogin() => this.LastLoginAt = DateTimeOffset.UtcNow;

    public void Deactivate() => this.IsActive = false;

    public void Activate() => this.IsActive = true;
}
