using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Domain.Tests.AppUserAggregate;

public class AppUserTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var orgId = Guid.CreateVersion7();
        var user = AppUser.Create("entra-oid-123", "jane@example.com", "Jane Smith", AppUserRole.Owner, orgId);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("entra-oid-123", user.IdentityId);
        Assert.Equal("jane@example.com", user.Email);
        Assert.Equal("Jane Smith", user.DisplayName);
        Assert.Equal(AppUserRole.Owner, user.Role);
        Assert.Equal(orgId, user.OrganizationId);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Null(user.LastLoginAt);
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
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Staff, Guid.CreateVersion7());

        user.RecordLogin();

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Staff, Guid.CreateVersion7());

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Staff, Guid.CreateVersion7());
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void AssignCourse_CreatesCourseAssignment()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Staff, Guid.CreateVersion7());
        var courseId = Guid.CreateVersion7();

        var assignment = user.AssignCourse(courseId);

        Assert.Equal(user.Id, assignment.AppUserId);
        Assert.Equal(courseId, assignment.CourseId);
        Assert.True(assignment.AssignedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.Contains(assignment, user.CourseAssignments);
    }

    [Fact]
    public void AssignCourse_DuplicateThrows()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Staff, Guid.CreateVersion7());
        var courseId = Guid.CreateVersion7();
        user.AssignCourse(courseId);

        Assert.Throws<InvalidOperationException>(() => user.AssignCourse(courseId));
    }

    [Fact]
    public void UnassignCourse_RemovesAssignment()
    {
        var user = AppUser.Create("oid", "e@e.com", "Test", AppUserRole.Staff, Guid.CreateVersion7());
        var courseId = Guid.CreateVersion7();
        user.AssignCourse(courseId);

        user.UnassignCourse(courseId);

        Assert.Empty(user.CourseAssignments);
    }
}
