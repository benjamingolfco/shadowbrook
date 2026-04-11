using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.AppUserAggregate.Exceptions;
using Teeforce.Domain.Common;
using Teeforce.Domain.Services;

namespace Teeforce.Domain.AppUserAggregate;

public class AppUser : Entity
{
    public string? IdentityId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public AppUserRole Role { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? InviteSentAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private AppUser() { } // EF

    public static async Task<AppUser> CreateAdmin(
        string email,
        IAppUserEmailUniquenessChecker emailChecker,
        bool sendInvite = false,
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
            ShouldSendInvite = sendInvite,
        });

        return user;
    }

    public static async Task<AppUser> CreateOperator(
        string email,
        Guid organizationId,
        IAppUserEmailUniquenessChecker emailChecker,
        bool sendInvite = false,
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
            ShouldSendInvite = sendInvite,
        });

        return user;
    }

    public bool IsIdentitySetupComplete => FirstName is not null;

    public void CompleteProfileSetup(string firstName, string lastName)
    {
        if (FirstName is not null)
        {
            return; // Idempotent — profile already populated
        }

        FirstName = firstName;
        LastName = lastName;
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
        var identityId = await invitationService.SendInvitationAsync(Email, ct);
        IdentityId = identityId;
        IsActive = true;
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

    public void Delete()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new AppUserDeleted
        {
            AppUserId = Id,
            IdentityId = IdentityId,
        });
    }
}
