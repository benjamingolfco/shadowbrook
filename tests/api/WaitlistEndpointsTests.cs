using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class WaitlistEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WaitlistEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateTestTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = $"Test Tenant {Guid.NewGuid()}",
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        return tenant!.Id;
    }

    private async Task<Guid> CreateTestCourseAsync(Guid tenantId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}" });
        var response = await _client.SendAsync(request);

        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        return course!.Id;
    }

    private async Task EnableWaitlistAsync(Guid courseId)
    {
        await _client.PutAsJsonAsync($"/courses/{courseId}/waitlist-settings", new
        {
            WaitlistEnabled = true
        });
    }

    // --- GET /courses/{courseId}/waitlist-settings ---

    [Fact]
    public async Task GetWaitlistSettings_DefaultsToFalse()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<WaitlistSettingsResponse>();
        Assert.NotNull(settings);
        Assert.False(settings!.WaitlistEnabled);
    }

    [Fact]
    public async Task UpdateWaitlistSettings_EnablesWaitlist()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);

        var putResponse = await _client.PutAsJsonAsync($"/courses/{courseId}/waitlist-settings", new
        {
            WaitlistEnabled = true
        });

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putResult = await putResponse.Content.ReadFromJsonAsync<WaitlistSettingsResponse>();
        Assert.True(putResult!.WaitlistEnabled);

        // Confirm GET reflects the change
        var getResponse = await _client.GetAsync($"/courses/{courseId}/waitlist-settings");
        var getResult = await getResponse.Content.ReadFromJsonAsync<WaitlistSettingsResponse>();
        Assert.True(getResult!.WaitlistEnabled);
    }

    [Fact]
    public async Task UpdateWaitlistSettings_DisablesWaitlist()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);

        // Enable first
        await _client.PutAsJsonAsync($"/courses/{courseId}/waitlist-settings", new { WaitlistEnabled = true });

        // Then disable
        var putResponse = await _client.PutAsJsonAsync($"/courses/{courseId}/waitlist-settings", new
        {
            WaitlistEnabled = false
        });

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putResult = await putResponse.Content.ReadFromJsonAsync<WaitlistSettingsResponse>();
        Assert.False(putResult!.WaitlistEnabled);
    }

    [Fact]
    public async Task GetWaitlistSettings_CourseNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/courses/{Guid.NewGuid()}/waitlist-settings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GET /courses/{courseId}/waitlist?date=... ---

    [Fact]
    public async Task GetWaitlist_WaitlistNotEnabled_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWaitlist_NoEntries_ReturnsEmptyList()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WaitlistResponse>();
        Assert.NotNull(result);
        Assert.Null(result!.CourseWaitlistId);
        Assert.Equal(0, result.TotalGolfersPending);
        Assert.Empty(result.Requests);
    }

    [Fact]
    public async Task GetWaitlist_WithEntries_ReturnsSummaryAndRequests()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        // Add two requests for different tee times
        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-11",
            TeeTime = "09:00",
            GolfersNeeded = 2
        });
        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-11",
            TeeTime = "08:00",
            GolfersNeeded = 3
        });

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WaitlistResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result!.CourseWaitlistId);
        Assert.Equal(5, result.TotalGolfersPending); // 2 + 3
        Assert.Equal(2, result.Requests.Count);

        // Verify ordered by tee time ascending
        Assert.Equal("08:00", result.Requests[0].TeeTime);
        Assert.Equal("09:00", result.Requests[1].TeeTime);
    }

    [Fact]
    public async Task GetWaitlist_MissingDate_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWaitlist_InvalidDateFormat_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist?date=03-02-2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWaitlist_CourseNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/courses/{Guid.NewGuid()}/waitlist?date=2026-03-02");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- POST /courses/{courseId}/waitlist/requests ---

    [Fact]
    public async Task CreateWaitlistRequest_ValidRequest_Returns201()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        var response = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "10:00",
            GolfersNeeded = 2
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WaitlistRequestResponse>();
        Assert.NotNull(result);
        Assert.Equal("10:00", result!.TeeTime);
        Assert.Equal(2, result.GolfersNeeded);
        Assert.Equal("Pending", result.Status);

        // Verify subsequent GET includes the new entry
        var getResponse = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-15");
        var waitlist = await getResponse.Content.ReadFromJsonAsync<WaitlistResponse>();
        Assert.Single(waitlist!.Requests);
    }

    [Fact]
    public async Task CreateWaitlistRequest_WaitlistNotEnabled_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);

        var response = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "10:00",
            GolfersNeeded = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_InvalidTeeTime_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        var response = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "invalid",
            GolfersNeeded = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_GolfersNeededOutOfRange_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        // Test 0
        var r0 = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "10:00",
            GolfersNeeded = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, r0.StatusCode);

        // Test 5
        var r5 = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "10:00",
            GolfersNeeded = 5
        });
        Assert.Equal(HttpStatusCode.BadRequest, r5.StatusCode);

        // Test -1
        var rNeg = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "10:00",
            GolfersNeeded = -1
        });
        Assert.Equal(HttpStatusCode.BadRequest, rNeg.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_DuplicateTeeTime_Returns409()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        // Create the first request
        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-16",
            TeeTime = "11:00",
            GolfersNeeded = 2
        });

        // Try to create a duplicate
        var response = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-16",
            TeeTime = "11:00",
            GolfersNeeded = 3
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_SameTeeTimeDifferentDate_Returns201()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        var r1 = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-17",
            TeeTime = "08:30",
            GolfersNeeded = 1
        });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-18",
            TeeTime = "08:30",
            GolfersNeeded = 1
        });
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_CourseNotFound_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/courses/{Guid.NewGuid()}/waitlist/requests", new
        {
            Date = "2026-03-15",
            TeeTime = "10:00",
            GolfersNeeded = 2
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_MissingDate_Returns400()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        // Post with null date (string type defaults to null when not provided)
        var response = await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = (string?)null,
            TeeTime = "10:00",
            GolfersNeeded = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWaitlistRequest_CreatesCorrespondingCourseWaitlist()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        // Verify empty before
        var beforeGet = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-20");
        var beforeResult = await beforeGet.Content.ReadFromJsonAsync<WaitlistResponse>();
        Assert.Null(beforeResult!.CourseWaitlistId);

        // Create a request
        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-20",
            TeeTime = "07:00",
            GolfersNeeded = 4
        });

        // Verify CourseWaitlist was created
        var afterGet = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-20");
        var afterResult = await afterGet.Content.ReadFromJsonAsync<WaitlistResponse>();
        Assert.NotNull(afterResult!.CourseWaitlistId);
    }

    [Fact]
    public async Task GetWaitlist_MultipleRequestsSameDate_CorrectTotalPending()
    {
        var tenantId = await CreateTestTenantAsync();
        var courseId = await CreateTestCourseAsync(tenantId);
        await EnableWaitlistAsync(courseId);

        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-21",
            TeeTime = "07:00",
            GolfersNeeded = 1
        });
        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-21",
            TeeTime = "07:10",
            GolfersNeeded = 4
        });
        await _client.PostAsJsonAsync($"/courses/{courseId}/waitlist/requests", new
        {
            Date = "2026-03-21",
            TeeTime = "07:20",
            GolfersNeeded = 2
        });

        var response = await _client.GetAsync($"/courses/{courseId}/waitlist?date=2026-03-21");
        var result = await response.Content.ReadFromJsonAsync<WaitlistResponse>();

        Assert.Equal(7, result!.TotalGolfersPending); // 1 + 4 + 2
        Assert.Equal(3, result.Requests.Count);
    }

    // Private response record types
    private record TenantResponse(Guid Id);
    private record CourseResponse(Guid Id, string Name);
    private record WaitlistSettingsResponse(bool WaitlistEnabled);
    private record WaitlistResponse(
        Guid? CourseWaitlistId,
        string Date,
        int TotalGolfersPending,
        List<WaitlistRequestResponse> Requests);
    private record WaitlistRequestResponse(
        Guid Id,
        string TeeTime,
        int GolfersNeeded,
        string Status);
}
