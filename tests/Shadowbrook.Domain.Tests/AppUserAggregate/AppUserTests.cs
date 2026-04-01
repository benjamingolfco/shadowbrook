using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.AppUserAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.AppUserAggregate;

public class AppUserTests
{
    [Fact]
    public void CreateAdmin_SetsAdminRoleAndNullOrganizationId()
    {
        var user = AppUser.CreateAdmin("entra-oid-admin", "admin@shadowbrook.com", "Admin");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("entra-oid-admin", user.IdentityId);
        Assert.Equal("admin@shadowbrook.com", user.Email);
        Assert.Equal("Admin", user.DisplayName);
        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public void CreateOperator_SetsOperatorRoleAndOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.CreateOperator("entra-oid-123", "jane@example.com", "Jane Smith", orgId);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("entra-oid-123", user.IdentityId);
        Assert.Equal("jane@example.com", user.Email);
        Assert.Equal("Jane Smith", user.DisplayName);
        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(orgId, user.OrganizationId);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public void RecordLogin_UpdatesLastLoginAt()
    {
        var user = AppUser.CreateOperator("oid", "e@e.com", "Test", Guid.CreateVersion7());

        user.RecordLogin();

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = AppUser.CreateOperator("oid", "e@e.com", "Test", Guid.CreateVersion7());

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = AppUser.CreateOperator("oid", "e@e.com", "Test", Guid.CreateVersion7());
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void MakeAdmin_SetsAdminRoleAndClearsOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.CreateOperator("oid", "e@e.com", "Test", orgId);

        user.MakeAdmin();

        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
    }

    [Fact]
    public void AssignToOrganization_SetsOperatorRoleAndOrganizationId()
    {
        var user = AppUser.CreateAdmin("oid", "e@e.com", "Test");
        var newOrgId = Guid.CreateVersion7();

        user.AssignToOrganization(newOrgId);

        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(newOrgId, user.OrganizationId);
    }

    [Fact]
    public void CreateOperator_WithEmptyGuid_Throws() =>
        Assert.Throws<EmptyOrganizationIdException>(() =>
            AppUser.CreateOperator("oid", "e@e.com", "Test", Guid.Empty));

    [Fact]
    public void AssignToOrganization_WithEmptyGuid_Throws()
    {
        var user = AppUser.CreateAdmin("oid", "e@e.com", "Test");

        Assert.Throws<EmptyOrganizationIdException>(() => user.AssignToOrganization(Guid.Empty));
    }
}
