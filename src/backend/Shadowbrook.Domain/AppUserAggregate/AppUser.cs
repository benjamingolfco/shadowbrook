using Shadowbrook.Domain.AppUserAggregate.Events;
using Shadowbrook.Domain.AppUserAggregate.Exceptions;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.Services;

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
    public DateTimeOffset? InviteSentAt { get; private set; }

    private AppUser() { } // EF

    public static async Task<AppUser> CreateAdmin(
        string email,
        IAppUserEmailUniquenessChecker emailChecker,
        CancellationToken ct = default)
    {
        if (await emailChecker.IsEmailInUse(email.Trim(), ct))
        {
            throw new DuplicateEmailException(email);
        }

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

    public static async Task<AppUser> CreateOperator(
        string email,
        Guid organizationId,
        IAppUserEmailUniquenessChecker emailChecker,
        CancellationToken ct = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new EmptyOrganizationIdException();
        }

        if (await emailChecker.IsEmailInUse(email.Trim(), ct))
        {
            throw new DuplicateEmailException(email);
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
        if (IdentityId is not null && IdentityId == identityId)
        {
            return; // Idempotent — already linked to this identity
        }

        if (IdentityId is not null)
        {
            throw new IdentityAlreadyLinkedException();
        }

        IdentityId = identityId;
        FirstName = firstName;
        LastName = lastName;
        IsActive = true;

        AddDomainEvent(new AppUserSetupCompleted
        {
            AppUserId = Id,
            Email = Email,
        });
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

    public async Task Invite(IAppUserInvitationService invitationService, CancellationToken ct)
    {
        if (IdentityId is not null || InviteSentAt is not null)
        {
            return;
        }

        var identityId = await invitationService.SendInvitationAsync(Email, ct);
        IdentityId = identityId;
        InviteSentAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new AppUserInvited
        {
            AppUserId = Id,
            Email = Email,
            EntraObjectId = identityId,
        });
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
