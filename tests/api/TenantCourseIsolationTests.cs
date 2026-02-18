using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class TenantCourseIsolationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TenantCourseIsolationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCourseById_FromDifferentTenant_ReturnsNotFound()
    {
        // Arrange - Create course for Tenant A
        var tenantAId = await CreateTestTenantAsync("Tenant A");
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Tenant A Course" });
        var createResponse = await _client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        // Act - Try to access from Tenant B
        var tenantBId = await CreateTestTenantAsync("Tenant B");
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/courses/{course!.Id}");
        getRequest.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        var response = await _client.SendAsync(getRequest);

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
        requestA.Content = JsonContent.Create(new { Name = "Tenant A Course 1" });
        await _client.SendAsync(requestA);

        var requestA2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestA2.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        requestA2.Content = JsonContent.Create(new { Name = "Tenant A Course 2" });
        await _client.SendAsync(requestA2);

        var requestB = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestB.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        requestB.Content = JsonContent.Create(new { Name = "Tenant B Course 1" });
        await _client.SendAsync(requestB);

        // Act - Get courses for Tenant A
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/courses");
        getRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        var response = await _client.SendAsync(getRequest);

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
        requestA.Content = JsonContent.Create(new { Name = "Admin Tenant A Course" });
        await _client.SendAsync(requestA);

        var requestB = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestB.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        requestB.Content = JsonContent.Create(new { Name = "Admin Tenant B Course" });
        await _client.SendAsync(requestB);

        // Act - Get all courses without tenant header (admin path)
        var response = await _client.GetAsync("/courses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await response.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.NotNull(courses);
        Assert.True(courses!.Count >= 2);
    }

    [Fact]
    public async Task CreateCourse_DuplicateName_SameTenant_ReturnsConflict()
    {
        // Arrange
        var tenantId = await CreateTestTenantAsync("Duplicate Test Tenant");
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request1.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request1.Content = JsonContent.Create(new { Name = "Duplicate Course" });
        await _client.SendAsync(request1);

        // Act - Try to create another course with the same name
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request2.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request2.Content = JsonContent.Create(new { Name = "Duplicate Course" });
        var response = await _client.SendAsync(request2);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("already exists", error!.Error);
    }

    [Fact]
    public async Task CreateCourse_DuplicateName_DifferentTenants_Succeeds()
    {
        // Arrange
        var tenantAId = await CreateTestTenantAsync("Tenant A Duplicate");
        var tenantBId = await CreateTestTenantAsync("Tenant B Duplicate");

        var requestA = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestA.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        requestA.Content = JsonContent.Create(new { Name = "Shared Name Course" });
        await _client.SendAsync(requestA);

        // Act - Create course with same name for different tenant
        var requestB = new HttpRequestMessage(HttpMethod.Post, "/courses");
        requestB.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        requestB.Content = JsonContent.Create(new { Name = "Shared Name Course" });
        var response = await _client.SendAsync(requestB);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCourse_WithoutTenantHeader_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/courses", new { Name = "No Tenant Course" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("X-Tenant-Id header is required", error!.Error);
    }

    [Fact]
    public async Task UpdateTeeTimeSettings_FromDifferentTenant_ReturnsNotFound()
    {
        // Arrange - Create course for Tenant A
        var tenantAId = await CreateTestTenantAsync("Tenant A Settings");
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantAId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Settings Test Course" });
        var createResponse = await _client.SendAsync(createRequest);
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
        var response = await _client.SendAsync(updateRequest);

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
        createRequest.Content = JsonContent.Create(new { Name = "Pricing Test Course" });
        var createResponse = await _client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        // Act - Try to update from Tenant B
        var tenantBId = await CreateTestTenantAsync("Tenant B Pricing");
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/courses/{course!.Id}/pricing");
        updateRequest.Headers.Add("X-Tenant-Id", tenantBId.ToString());
        updateRequest.Content = JsonContent.Create(new { FlatRatePrice = 45.00m });
        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateTestTenantAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/tenants", new
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
