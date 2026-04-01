using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Domain.Tests.AppUserAggregate;

public class AppUserTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.Create("entra-oid-123", "jane@example.com", "Jane Smith", AppUserRole.Operator, orgId);

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
    public void Create_WithOperatorRole_SetsOrganizationId()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.Create("oid-1", "op@test.com", "Operator", AppUserRole.Operator, orgId);

        Assert.Equal(AppUserRole.Operator, user.Role);
        Assert.Equal(orgId, user.OrganizationId);
    }

    [Fact]
    public void Create_Admin_HasNullOrganizationId()
    {
        var user = AppUser.Create("entra-oid-admin", "admin@shadowbrook.com", "Admin", AppUserRole.Admin, organizationId: null);

        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Null(user.OrganizationId);
    }

    [Fact]
    public void RecordLogin_UpdatesLastLoginAt()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Operator, Guid.CreateVersion7());

        user.RecordLogin();

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Operator, Guid.CreateVersion7());

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Operator, Guid.CreateVersion7());
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void UpdateRole_UpdatesRoleAndOrganizationId()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Operator, Guid.CreateVersion7());
        var newOrgId = Guid.CreateVersion7();

        user.UpdateRole(AppUserRole.Admin, newOrgId);

        Assert.Equal(AppUserRole.Admin, user.Role);
        Assert.Equal(newOrgId, user.OrganizationId);
    }
}
