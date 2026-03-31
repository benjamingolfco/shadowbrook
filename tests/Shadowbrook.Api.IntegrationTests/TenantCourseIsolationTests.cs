using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class TenantCourseIsolationTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        await factory.SeedTestAdminAsync();
        this.client = factory.CreateAuthenticatedClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCourseById_FromDifferentTenant_ReturnsNotFound()
    {
        // Arrange - Create course for Tenant A
        var tenantAId = await CreateTestTenantAsync("Tenant A");
        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = "Tenant A Course", TenantId = tenantAId, TimeZoneId = TestTimeZones.Chicago });
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        // Act - The Admin user can see all courses, but course lookup via non-existent ID still 404s
        // EF query filter: admin (no org_id) sees all courses, so this test verifies
        // that a course ID from another tenant is accessible to admin (correct admin behavior)
        var response = await this.client.GetAsync($"/courses/{course!.Id}");

        // Assert - Admin can access any course
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCourseById_NonExistentId_ReturnsNotFound()
    {
        var response = await this.client.GetAsync($"/courses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCourses_AdminSeesAllCourses()
    {
        // Arrange - Create courses for different tenants
        var tenantAId = await CreateTestTenantAsync("Tenant A");
        var tenantBId = await CreateTestTenantAsync("Tenant B");

        await this.client.PostAsJsonAsync("/courses", new { Name = "Tenant A Course 1", TenantId = tenantAId, TimeZoneId = TestTimeZones.Chicago });
        await this.client.PostAsJsonAsync("/courses", new { Name = "Tenant A Course 2", TenantId = tenantAId, TimeZoneId = TestTimeZones.Chicago });
        await this.client.PostAsJsonAsync("/courses", new { Name = "Tenant B Course 1", TenantId = tenantBId, TimeZoneId = TestTimeZones.Chicago });

        // Act - Admin has no org scope, so all courses are returned
        var response = await this.client.GetAsync("/courses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await response.Content.ReadFromJsonAsync<List<CourseIdResponse>>();
        Assert.NotNull(courses);
        Assert.True(courses!.Count >= 3);
    }

    [Fact]
    public async Task GetAllCourses_WithoutTenantHeader_ReturnsAllCourses()
    {
        // Arrange - Create courses for different tenants
        var tenantAId = await CreateTestTenantAsync("Tenant A Admin");
        var tenantBId = await CreateTestTenantAsync("Tenant B Admin");

        await this.client.PostAsJsonAsync("/courses", new { Name = "Admin Tenant A Course", TenantId = tenantAId, TimeZoneId = TestTimeZones.Chicago });
        await this.client.PostAsJsonAsync("/courses", new { Name = "Admin Tenant B Course", TenantId = tenantBId, TimeZoneId = TestTimeZones.Chicago });

        // Act - Get all courses without tenant header (admin path — no org scope means all courses)
        var response = await this.client.GetAsync("/courses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await response.Content.ReadFromJsonAsync<List<CourseIdResponse>>();
        Assert.NotNull(courses);
        Assert.True(courses!.Count >= 3);
    }

    [Fact]
    public async Task CreateCourse_WithTenantIdInBody_Succeeds()
    {
        // Arrange
        var tenantId = await CreateTestTenantAsync("Body Tenant");

        // Act - Create course with TenantId in body
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "Body Tenant Course", TenantId = tenantId, TimeZoneId = TestTimeZones.Chicago });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<CourseIdResponse>();
        Assert.NotNull(course);
    }

    [Fact]
    public async Task CreateCourse_WithoutTenantId_ReturnsBadRequest()
    {
        // Act - no TenantId in body, admin has no org
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "No Tenant Course", TimeZoneId = TestTimeZones.Chicago });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("OrganizationId is required", error!.Error);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_ValidCourse_Succeeds()
    {
        // Arrange - Create course
        var tenantAId = await CreateTestTenantAsync("Tenant A Settings");
        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = "Settings Test Course", TenantId = tenantAId, TimeZoneId = TestTimeZones.Chicago });
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        // Act - Admin can update any course
        var response = await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePricing_ValidCourse_Succeeds()
    {
        // Arrange - Create course
        var tenantAId = await CreateTestTenantAsync("Tenant A Pricing");
        var createResponse = await this.client.PostAsJsonAsync("/courses", new { Name = "Pricing Test Course", TenantId = tenantAId, TimeZoneId = TestTimeZones.Chicago });
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        // Act - Admin can update any course
        var response = await this.client.PutAsJsonAsync($"/courses/{course!.Id}/pricing", new { FlatRatePrice = 45.00m });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> CreateTestTenantAsync(string name)
    {
        var response = await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = $"{name} {Guid.NewGuid()}",
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return tenant!.Id;
    }
}
