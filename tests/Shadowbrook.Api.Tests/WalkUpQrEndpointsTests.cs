using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
[IntegrationTest]
public class WalkUpQrEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // GET /walkup/status/{shortCode}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Status_OpenWaitlist_ReturnsOpen()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();

        var response = await GetStatusAsync(shortCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpQrStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("open", body!.Status);
        Assert.NotNull(body.CourseName);
        Assert.NotEmpty(body.CourseName);
        Assert.NotNull(body.Date);
    }

    [Fact]
    public async Task Status_ClosedWaitlist_ReturnsClosed()
    {
        var (_, courseId, shortCode) = await CreateOpenWaitlistAsync();
        await PostCloseWaitlistAsync(courseId);

        var response = await GetStatusAsync(shortCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpQrStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("closed", body!.Status);
        Assert.NotNull(body.CourseName);
        Assert.NotNull(body.Date);
    }

    [Fact]
    public async Task Status_InvalidCode_Returns404()
    {
        var response = await GetStatusAsync("0000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("This QR code is not valid.", body!.Error);
    }

    [Fact]
    public async Task Status_NoTenantHeader_StillWorks()
    {
        // Create an open waitlist via normal flow (with tenant header)
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();

        // Create a new client with no default headers
        var publicClient = factory.CreateClient();

        // GET the status endpoint WITHOUT setting X-Tenant-Id header
        var response = await publicClient.GetAsync($"/walkup/status/{shortCode}");

        // Should still work since endpoint uses IgnoreQueryFilters
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpQrStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("open", body!.Status);
    }

    [Fact]
    public async Task Status_ReturnsCorrectCourseName()
    {
        var tenantId = await CreateTestTenantAsync();
        var expectedName = $"QR Test Course {Guid.NewGuid()}";

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = expectedName, TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        var openResponse = await PostOpenWaitlistAsync(course!.Id);
        var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        var statusResponse = await GetStatusAsync(waitlist!.ShortCode);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<WalkUpQrStatusResponse>();

        Assert.Equal(expectedName, statusBody!.CourseName);
    }

    [Fact]
    public async Task Status_ReturnsCorrectDate()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();

        var response = await GetStatusAsync(shortCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpQrStatusResponse>();
        Assert.NotNull(body);

        // Date should be in ISO format (yyyy-MM-dd)
        Assert.Matches(@"\d{4}-\d{2}-\d{2}", body!.Date);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HttpResponseMessage> GetStatusAsync(string shortCode) =>
        await this.client.GetAsync($"/walkup/status/{shortCode}");

    private async Task<HttpResponseMessage> PostOpenWaitlistAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        return await this.client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostCloseWaitlistAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/close");
        return await this.client.SendAsync(request);
    }

    private async Task<(Guid TenantId, Guid CourseId, string ShortCode)> CreateOpenWaitlistAsync()
    {
        var tenantId = await CreateTestTenantAsync();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        var openResponse = await PostOpenWaitlistAsync(course!.Id);
        var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        return (tenantId, course.Id, waitlist!.ShortCode);
    }

    private async Task<Guid> CreateTestTenantAsync()
    {
        var response = await this.client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = $"Test Tenant {Guid.NewGuid()}",
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return tenant!.Id;
    }

    private record WalkUpQrStatusResponse(string Status, string CourseName, string Date);
    private record ErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
    private record WalkUpWaitlistResponse(Guid Id, string ShortCode);
}
