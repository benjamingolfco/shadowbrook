using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.OrganizationAggregate;
using Teeforce.Domain.Services;

namespace Teeforce.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class CourseAccessIsolationTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Operator_CanGetCourse_InOwnOrganization()
    {
        // Arrange: create a tenant + course via admin, then seed an operator in the same org
        await factory.SeedTestAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient();
        var tenantId = await TestSetup.CreateTenantAsync(adminClient);
        var courseResponse = await adminClient.PostAsJsonAsync("/courses", new
        {
            Name = "Operator Own Course",
            OrganizationId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        courseResponse.EnsureSuccessStatusCode();
        var courseId = (await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>())!.Id;

        // Seed operator in the same org
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailChecker = scope.ServiceProvider.GetRequiredService<IAppUserEmailUniquenessChecker>();
        var operatorUser = await AppUser.CreateOperator("op@own.com", tenantId, emailChecker);
        operatorUser.Activate();
        db.AppUsers.Add(operatorUser);
        await db.SaveChangesAsync();

        var opClient = factory.CreateAuthenticatedClient("op@own.com");

        // Act
        var response = await opClient.GetAsync($"/courses/{courseId}");

        // Assert: Operator can see their own org's course
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CannotGetCourse_InDifferentOrganization()
    {
        // Arrange: Org A has Course A, Org B has Course B. Operator user belongs to Org A.
        // Authorization runs before data lookup, so no Tenant rows are needed.
        var (_, courseBId) = await SeedTwoOrgsWithOperatorAsync("cross-get@orga.com");
        var client = factory.CreateAuthenticatedClient("cross-get@orga.com");

        // Act
        var response = await client.GetAsync($"/courses/{courseBId}");

        // Assert: Operator A cannot see Course B (different org) — EF query filter makes it invisible
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CannotUpdateTeeTimeSettings_ForCourse_InDifferentOrganization()
    {
        // Arrange: Org A has Course A, Org B has Course B. Operator user belongs to Org A.
        // Authorization runs before data lookup, so no Tenant rows are needed.
        var (_, courseBId) = await SeedTwoOrgsWithOperatorAsync("cross-put@orga.com");
        var client = factory.CreateAuthenticatedClient("cross-put@orga.com");

        // Act
        var response = await client.PutAsJsonAsync($"/courses/{courseBId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });

        // Assert: Operator A cannot see Course B (different org) — EF query filter + CourseExistsMiddleware returns 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            OrganizationId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        courseResponse.EnsureSuccessStatusCode();
        var courseId = (await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>())!.Id;

        // Seed a second admin to verify admins can access courses they didn't create.
        await factory.SeedTestAdminAsync("admin-b@test.com");
        var adminBClient = factory.CreateAuthenticatedClient("admin-b@test.com");

        // Act
        var response = await adminBClient.GetAsync($"/courses/{courseId}");

        // Assert: Admin can access any course regardless of org
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Seeds Org A with Course A, Org B with Course B, and an Operator user in Org A.
    /// Returns (courseAId, courseBId). No Tenant rows — authorization fails before any data lookup.
    /// </summary>
    private async Task<(Guid CourseAId, Guid CourseBId)> SeedTwoOrgsWithOperatorAsync(string operatorEmail)
    {
        var orgA = Organization.Create($"Org-A-{operatorEmail}");
        var orgB = Organization.Create($"Org-B-{operatorEmail}");
        var courseA = Course.Create(orgA.Id, $"Course-A-{operatorEmail}", TestTimeZones.Chicago);
        var courseB = Course.Create(orgB.Id, $"Course-B-{operatorEmail}", TestTimeZones.Chicago);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailChecker = scope.ServiceProvider.GetRequiredService<IAppUserEmailUniquenessChecker>();
        var operatorA = await AppUser.CreateOperator(operatorEmail, orgA.Id, emailChecker);
        operatorA.Activate();

        db.Organizations.AddRange(orgA, orgB);
        db.Courses.AddRange(courseA, courseB);
        db.AppUsers.Add(operatorA);
        await db.SaveChangesAsync();

        return (courseA.Id, courseB.Id);
    }
}
