using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.AppUserAggregate.Events;
using Shadowbrook.Domain.AppUserAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.AppUserAggregate;

public class AppUserTests
{
    [Fact]
    public void CreateAdmin_SetsAdminRoleAndNullOrganizationId()
    {
        var user = AppUser.CreateAdmin("admin@shadowbrook.com");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Null(user.IdentityId);
        Assert.Equal("admin@shadowbrook.com", user.Email);
        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
        Assert.False(user.IsActive);
        Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Contains(user.DomainEvents, e => e is AppUserCreated);
    }

    [Fact]
    public void CreateOperator_SetsOperatorRoleAndOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.CreateOperator("jane@example.com", orgId);

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
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = AppUser.CreateOperator("e@e.com", Guid.CreateVersion7());
        user.CompleteIdentitySetup("oid", "Test", "User");

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = AppUser.CreateOperator("e@e.com", Guid.CreateVersion7());
        user.CompleteIdentitySetup("oid", "Test", "User");
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void MakeAdmin_SetsAdminRoleAndClearsOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.CreateOperator("e@e.com", orgId);

        user.MakeAdmin();

        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
    }

    [Fact]
    public void AssignToOrganization_SetsOperatorRoleAndOrganizationId()
    {
        var user = AppUser.CreateAdmin("e@e.com");
        var newOrgId = Guid.CreateVersion7();

        user.AssignToOrganization(newOrgId);

        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(newOrgId, user.OrganizationId);
    }

    [Fact]
    public void CreateOperator_WithEmptyGuid_Throws() =>
        Assert.Throws<EmptyOrganizationIdException>(() =>
            AppUser.CreateOperator("e@e.com", Guid.Empty));

    [Fact]
    public void AssignToOrganization_WithEmptyGuid_Throws()
    {
        var user = AppUser.CreateAdmin("e@e.com");

        Assert.Throws<EmptyOrganizationIdException>(() => user.AssignToOrganization(Guid.Empty));
    }

    [Fact]
    public void CompleteIdentitySetup_SetsIdentityAndActivates()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.ClearDomainEvents();

        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

        Assert.Equal("entra-oid-123", user.IdentityId);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.True(user.IsActive);
        Assert.Contains(user.DomainEvents, e => e is AppUserSetupCompleted);
    }

    [Fact]
    public void CompleteIdentitySetup_SameOid_IsIdempotent()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        user.ClearDomainEvents();

        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

        Assert.Empty(user.DomainEvents);
        Assert.Equal("entra-oid-123", user.IdentityId);
    }

    [Fact]
    public void CompleteIdentitySetup_DifferentOid_Throws()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");

        Assert.Throws<IdentityAlreadyLinkedException>(
            () => user.CompleteIdentitySetup("different-oid", "Jane", "Smith"));
    }
}
