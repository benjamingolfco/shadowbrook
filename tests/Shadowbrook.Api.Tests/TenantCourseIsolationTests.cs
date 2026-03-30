using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
[IntegrationTest]
public class TenantCourseIsolationTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCourseById_FromDifferentTenant_ReturnsNotFound()
    {
        // Arrange - Create course for Tenant A
        var tenantAId = await CreateTestTenantAsync("Tenant A");
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Tenant A Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        // Act - Try to access from Tenant B
        var tenantBId = await CreateTestTenantAsync("Tenant B");
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/courses/{course!.Id}");
        getRequest.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        var response = await this.client.SendAsync(getRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCourses_OnlyReturnsTenantCourses()
    {
        // Arrange - Create courses for different tenants
        var tenantAId = await CreateTestTenantAsync("Tenant A");
        var tenantBId = await CreateTestTenantAsync("Tenant B");

        var requestA = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestA.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        requestA.Content = JsonContent.Create(new { Name = "Tenant A Course 1", TimeZoneId = TestTimeZones.Chicago });
        await this.client.SendAsync(requestA);

        var requestA2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestA2.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        requestA2.Content = JsonContent.Create(new { Name = "Tenant A Course 2", TimeZoneId = TestTimeZones.Chicago });
        await this.client.SendAsync(requestA2);

        var requestB = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestB.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        requestB.Content = JsonContent.Create(new { Name = "Tenant B Course 1", TimeZoneId = TestTimeZones.Chicago });
        await this.client.SendAsync(requestB);

        // Act - Get courses for Tenant A
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/courses");
        getRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        var response = await this.client.SendAsync(getRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.NotNull(courses);
        Assert.Equal(2, courses!.Count);
        Assert.All(courses, c => Assert.Contains("Tenant A", c.Name));
    }

    [Fact]
    public async Task GetAllCourses_WithoutTenantHeader_ReturnsAllCourses()
    {
        // Arrange - Create courses for different tenants
        var tenantAId = await CreateTestTenantAsync("Tenant A Admin");
        var tenantBId = await CreateTestTenantAsync("Tenant B Admin");

        var requestA = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestA.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        requestA.Content = JsonContent.Create(new { Name = "Admin Tenant A Course", TimeZoneId = TestTimeZones.Chicago });
        await this.client.SendAsync(requestA);

        var requestB = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestB.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        requestB.Content = JsonContent.Create(new { Name = "Admin Tenant B Course", TimeZoneId = TestTimeZones.Chicago });
        await this.client.SendAsync(requestB);

        // Act - Get all courses without tenant header (admin path)
        var response = await this.client.GetAsync("/courses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.NotNull(courses);
        Assert.True(courses!.Count >= 2);
    }

    [Fact]
    public async Task CreateCourse_WithoutTenantHeaderOrBody_ReturnsBadRequest()
    {
        // Act
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "No Tenant Course", TimeZoneId = TestTimeZones.Chicago });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("OrganizationId is required", error!.Error);
    }

    [Fact]
    public async Task CreateCourse_WithTenantIdInBody_Succeeds()
    {
        // Arrange
        var tenantId = await CreateTestTenantAsync("Body Tenant");

        // Act - Create course with TenantId in body (no header)
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = "Body Tenant Course", TenantId = tenantId, TimeZoneId = TestTimeZones.Chicago });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal("Body Tenant Course", course!.Name);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_FromDifferentTenant_ReturnsNotFound()
    {
        // Arrange - Create course for Tenant A
        var tenantAId = await CreateTestTenantAsync("Tenant A Settings");
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Settings Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        // Act - Try to update from Tenant B
        var tenantBId = await CreateTestTenantAsync("Tenant B Settings");
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/courses/{course!.Id}/tee-time-settings");
        updateRequest.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        updateRequest.Content = JsonContent.Create(new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "18:00"
        });
        var response = await this.client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePricing_FromDifferentTenant_ReturnsNotFound()
    {
        // Arrange - Create course for Tenant A
        var tenantAId = await CreateTestTenantAsync("Tenant A Pricing");
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Pricing Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        // Act - Try to update from Tenant B
        var tenantBId = await CreateTestTenantAsync("Tenant B Pricing");
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/courses/{course!.Id}/pricing");
        updateRequest.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        updateRequest.Content = JsonContent.Create(new { FlatRatePrice = 45.00m });
        var response = await this.client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        return tenant!.Id;
    }

    private record CourseResponse(Guid Id, string Name);
    private record TenantResponse(Guid Id);
    private record ErrorResponse(string Error);
}
