using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.OrganizationAggregate;

namespace Shadowbrook.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class CourseAccessIsolationTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Owner_CannotGetCourse_InDifferentOrganization()
    {
        // Arrange: Org A has Course A, Org B has Course B. Owner user belongs to Org A.
        // Authorization runs before data lookup, so no Tenant rows are needed.
        var (_, courseBId) = await SeedTwoOrgsWithOwnerAsync("owner-cross-org-get");
        var client = factory.CreateAuthenticatedClient("owner-cross-org-get");

        // Act
        var response = await client.GetAsync($"/courses/{courseBId}");

        // Assert: Owner A is forbidden from Course B (different org)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CannotUpdateTeeTimeSettings_ForCourse_InDifferentOrganization()
    {
        // Arrange: Org A has Course A, Org B has Course B. Owner user belongs to Org A.
        // Authorization runs before data lookup, so no Tenant rows are needed.
        var (_, courseBId) = await SeedTwoOrgsWithOwnerAsync("owner-cross-org-put");
        var client = factory.CreateAuthenticatedClient("owner-cross-org-put");

        // Act
        var response = await client.PutAsJsonAsync($"/courses/{courseBId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });

        // Assert: Owner A is forbidden from modifying Course B (different org)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_CannotGetCourse_NotAssignedTo()
    {
        // Arrange: One org with Course X and Course Y. Staff is assigned to Course X only.
        // Authorization runs before data lookup, so no Tenant rows are needed.
        var (_, courseYId) = await SeedOrgWithStaffAsync("staff-unassigned");
        var client = factory.CreateAuthenticatedClient("staff-unassigned");

        // Act
        var response = await client.GetAsync($"/courses/{courseYId}");

        // Assert: Staff is forbidden from Course Y (not assigned)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_CanGetCourse_AssignedTo()
    {
        // Arrange: create a tenant + course via HTTP so Tenant and Organization rows exist for the
        // inner JOIN in GetCourseById, then seed the staff user with an assignment to that course.
        await factory.SeedTestAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient();
        var tenantId = await TestSetup.CreateTenantAsync(adminClient);
        var courseResponse = await adminClient.PostAsJsonAsync("/courses", new
        {
            Name = "Staff Control Course",
            TenantId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        courseResponse.EnsureSuccessStatusCode();
        var courseXId = (await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>())!.Id;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure an Organization row exists with the tenant's ID (transitional bridge).
        if (!await db.Organizations.AnyAsync(o => o.Id == tenantId))
        {
            db.Organizations.Add(Organization.CreateWithId(tenantId, $"Staff Org {tenantId}"));
        }

        var staff = AppUser.Create("staff-assigned", "staff@org.com", "Staff User", AppUserRole.Staff, tenantId);
        staff.AssignCourse(courseXId);
        db.AppUsers.Add(staff);
        await db.SaveChangesAsync();

        var staffClient = factory.CreateAuthenticatedClient("staff-assigned");

        // Act
        var response = await staffClient.GetAsync($"/courses/{courseXId}");

        // Assert: Staff can access the course they are assigned to
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanGetAnyCourse()
    {
        // Arrange: admin A creates a tenant + course via HTTP so Tenant and Organization rows exist
        // for the inner JOIN in GetCourseById.
        await factory.SeedTestAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient();
        var tenantId = await TestSetup.CreateTenantAsync(adminClient);
        var courseResponse = await adminClient.PostAsJsonAsync("/courses", new
        {
            Name = "Admin Visibility Course",
            TenantId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        courseResponse.EnsureSuccessStatusCode();
        var courseId = (await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>())!.Id;

        // Seed a second admin to verify admins can access courses they didn't create.
        await factory.SeedTestAdminAsync("admin-b-oid");
        var adminBClient = factory.CreateAuthenticatedClient("admin-b-oid");

        // Act
        var response = await adminBClient.GetAsync($"/courses/{courseId}");

        // Assert: Admin can access any course regardless of org
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Seeds Org A with Course A, Org B with Course B, and an Owner user in Org A.
    /// Returns (courseAId, courseBId). No Tenant rows — authorization fails before any data lookup.
    /// </summary>
    private async Task<(Guid CourseAId, Guid CourseBId)> SeedTwoOrgsWithOwnerAsync(string ownerIdentityId)
    {
        var orgA = Organization.Create($"Org-A-{ownerIdentityId}");
        var orgB = Organization.Create($"Org-B-{ownerIdentityId}");
        var courseA = Course.Create(orgA.Id, $"Course-A-{ownerIdentityId}", TestTimeZones.Chicago);
        var courseB = Course.Create(orgB.Id, $"Course-B-{ownerIdentityId}", TestTimeZones.Chicago);
        var ownerA = AppUser.Create(ownerIdentityId, "owner@orga.com", "Owner A", AppUserRole.Owner, orgA.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Organizations.AddRange(orgA, orgB);
        db.Courses.AddRange(courseA, courseB);
        db.AppUsers.Add(ownerA);
        await db.SaveChangesAsync();

        return (courseA.Id, courseB.Id);
    }

    /// <summary>
    /// Seeds one Org with Course X and Course Y, and a Staff user assigned to Course X only.
    /// Returns (courseXId, courseYId). No Tenant rows — authorization fails before any data lookup.
    /// </summary>
    private async Task<(Guid CourseXId, Guid CourseYId)> SeedOrgWithStaffAsync(string staffIdentityId)
    {
        var org = Organization.Create($"Staff-Org-{staffIdentityId}");
        var courseX = Course.Create(org.Id, $"Course-X-{staffIdentityId}", TestTimeZones.Chicago);
        var courseY = Course.Create(org.Id, $"Course-Y-{staffIdentityId}", TestTimeZones.Chicago);
        var staff = AppUser.Create(staffIdentityId, "staff@org.com", "Staff User", AppUserRole.Staff, org.Id);
        staff.AssignCourse(courseX.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Organizations.Add(org);
        db.Courses.AddRange(courseX, courseY);
        db.AppUsers.Add(staff);
        await db.SaveChangesAsync();

        return (courseX.Id, courseY.Id);
    }
}
