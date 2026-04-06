using NSubstitute;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.AppUserAggregate.Exceptions;
using Teeforce.Domain.Services;

namespace Teeforce.Domain.Tests.AppUserAggregate;

public class AppUserTests
{
    private static IAppUserEmailUniquenessChecker NewChecker(bool emailInUse = false)
    {
        var checker = Substitute.For<IAppUserEmailUniquenessChecker>();
        checker.IsEmailInUse(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(emailInUse);
        return checker;
    }

    [Fact]
    public async Task CreateAdmin_SetsAdminRoleAndNullOrganizationId()
    {
        var user = await AppUser.CreateAdmin("admin@benjamingolfco.onmicrosoft.com", NewChecker());

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Null(user.IdentityId);
        Assert.Equal("admin@benjamingolfco.onmicrosoft.com", user.Email);
        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
        Assert.False(user.IsActive);
        Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Contains(user.DomainEvents, e => e is AppUserCreated);
        var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
        Assert.False(createdEvent.ShouldSendInvite);
    }

    [Fact]
    public async Task CreateOperator_SetsOperatorRoleAndOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = await AppUser.CreateOperator("jane@example.com", orgId, NewChecker());

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Null(user.IdentityId);
        Assert.Equal("jane@example.com", user.Email);
        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(orgId, user.OrganizationId);
        Assert.False(user.IsActive);
        Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Contains(user.DomainEvents, e => e is AppUserCreated);
        var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
        Assert.False(createdEvent.ShouldSendInvite);
    }

    [Fact]
    public async Task CreateAdmin_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        await Assert.ThrowsAsync<DuplicateEmailException>(
            () => AppUser.CreateAdmin("admin@benjamingolfco.onmicrosoft.com", NewChecker(emailInUse: true)));
    }

    [Fact]
    public async Task CreateOperator_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        await Assert.ThrowsAsync<DuplicateEmailException>(
            () => AppUser.CreateOperator("jane@example.com", Guid.CreateVersion7(), NewChecker(emailInUse: true)));
    }

    [Fact]
    public async Task CreateAdmin_UniqueEmail_Succeeds()
    {
        var user = await AppUser.CreateAdmin("unique@example.com", NewChecker(emailInUse: false));

        Assert.Equal("unique@example.com", user.Email);
        Assert.Equal(AppUserRole.Admin, user.Role);
    }

    [Fact]
    public async Task CreateOperator_UniqueEmail_Succeeds()
    {
        var orgId = Guid.CreateVersion7();
        var user = await AppUser.CreateOperator("unique@example.com", orgId, NewChecker(emailInUse: false));

        Assert.Equal("unique@example.com", user.Email);
        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(orgId, user.OrganizationId);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var user = await AppUser.CreateOperator("e@e.com", Guid.CreateVersion7(), NewChecker());
        user.CompleteIdentitySetup("oid", "Test", "User");

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task Activate_SetsIsActiveTrue()
    {
        var user = await AppUser.CreateOperator("e@e.com", Guid.CreateVersion7(), NewChecker());
        user.CompleteIdentitySetup("oid", "Test", "User");
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task MakeAdmin_SetsAdminRoleAndClearsOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = await AppUser.CreateOperator("e@e.com", orgId, NewChecker());

        user.MakeAdmin();

        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
    }

    [Fact]
    public async Task AssignToOrganization_SetsOperatorRoleAndOrganizationId()
    {
        var user = await AppUser.CreateAdmin("e@e.com", NewChecker());
        var newOrgId = Guid.CreateVersion7();

        user.AssignToOrganization(newOrgId);

        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(newOrgId, user.OrganizationId);
    }

    [Fact]
    public async Task CreateOperator_WithEmptyGuid_Throws() =>
        await Assert.ThrowsAsync<EmptyOrganizationIdException>(
            () => AppUser.CreateOperator("e@e.com", Guid.Empty, NewChecker()));

    [Fact]
    public async Task AssignToOrganization_WithEmptyGuid_Throws()
    {
        var user = await AppUser.CreateAdmin("e@e.com", NewChecker());

        Assert.Throws<EmptyOrganizationIdException>(() => user.AssignToOrganization(Guid.Empty));
    }

    [Fact]
    public async Task CompleteIdentitySetup_SetsIdentityAndActivates()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.ClearDomainEvents();

        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

        Assert.Equal("entra-oid-123", user.IdentityId);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.True(user.IsActive);
        Assert.Contains(user.DomainEvents, e => e is AppUserSetupCompleted);
    }

    [Fact]
    public async Task CompleteIdentitySetup_SameOid_IsIdempotent()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        user.ClearDomainEvents();

        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

        Assert.Empty(user.DomainEvents);
        Assert.Equal("entra-oid-123", user.IdentityId);
    }

    [Fact]
    public async Task CompleteIdentitySetup_DifferentOid_Throws()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

        Assert.Throws<IdentityAlreadyLinkedException>(
            () => user.CompleteIdentitySetup("different-oid", "Jane", "Smith"));
    }

    [Fact]
    public async Task CreateAdmin_WithSendInvite_SetsEventFlag()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker(), sendInvite: true);

        var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
        Assert.True(createdEvent.ShouldSendInvite);
    }

    [Fact]
    public async Task CreateOperator_WithSendInvite_SetsEventFlag()
    {
        var user = await AppUser.CreateOperator("op@example.com", Guid.CreateVersion7(), NewChecker(), sendInvite: true);

        var createdEvent = user.DomainEvents.OfType<AppUserCreated>().Single();
        Assert.True(createdEvent.ShouldSendInvite);
    }

    [Fact]
    public async Task Delete_SetsIsDeletedAndDeletedAtAndRaisesEvent()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        user.ClearDomainEvents();

        user.Delete();

        Assert.True(user.IsDeleted);
        Assert.NotNull(user.DeletedAt);
        Assert.True(user.DeletedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        var deletedEvent = user.DomainEvents.OfType<AppUserDeleted>().Single();
        Assert.Equal(user.Id, deletedEvent.AppUserId);
        Assert.Equal("entra-oid-123", deletedEvent.IdentityId);
    }

    [Fact]
    public async Task Delete_WithoutIdentityId_EventHasNullIdentityId()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.ClearDomainEvents();

        user.Delete();

        Assert.True(user.IsDeleted);
        var deletedEvent = user.DomainEvents.OfType<AppUserDeleted>().Single();
        Assert.Null(deletedEvent.IdentityId);
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_IsIdempotent()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.Delete();
        user.ClearDomainEvents();

        user.Delete();

        Assert.Empty(user.DomainEvents);
        Assert.True(user.IsDeleted);
    }
}
