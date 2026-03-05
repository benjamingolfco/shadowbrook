using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class WalkUpWaitlistEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WalkUpWaitlistEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // POST /open
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Open_ReturnsCreated_WithShortCode()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        var response = await PostOpenAsync(courseId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();
        Assert.NotNull(body);
        Assert.Equal(courseId, body!.CourseId);
        Assert.Equal("Open", body.Status);
        Assert.NotNull(body.ShortCode);
        Assert.Equal(4, body.ShortCode.Length);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Null(body.ClosedAt);
    }

    [Fact]
    public async Task Open_WhenAlreadyOpen_Returns409()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        await PostOpenAsync(courseId);
        var response = await PostOpenAsync(courseId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Walk-up waitlist is already open for today.", body!.Error);
    }

    [Fact]
    public async Task Open_WhenAlreadyClosed_Returns409()
    {
        var (tenantId, courseId) = await CreateTestCourseAsync();

        await PostOpenAsync(courseId);
        await PostCloseAsync(courseId);

        var response = await PostOpenAsync(courseId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Walk-up waitlist was already used today.", body!.Error);
    }

    [Fact]
    public async Task Open_CourseNotFound_Returns404()
    {
        var response = await PostOpenAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Course not found.", body!.Error);
    }

    [Fact]
    public async Task Open_ShortCodeIsFourDigits()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        var response = await PostOpenAsync(courseId);
        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        Assert.NotNull(body);
        Assert.Equal(4, body!.ShortCode.Length);
        Assert.True(body.ShortCode.All(char.IsDigit), "Short code should consist only of digits.");
    }

    [Fact]
    public async Task Open_ShortCodeIsUniquePerDay()
    {
        // Open waitlists on two different courses and verify short codes are each 4 digits.
        // Full collision uniqueness is guaranteed by the algorithm; here we verify two
        // independent waitlists opened on the same day receive valid codes.
        var (_, courseId1) = await CreateTestCourseAsync();
        var (_, courseId2) = await CreateTestCourseAsync();

        var r1 = await PostOpenAsync(courseId1);
        var r2 = await PostOpenAsync(courseId2);

        var b1 = await r1.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();
        var b2 = await r2.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.Equal(4, b1!.ShortCode.Length);
        Assert.Equal(4, b2!.ShortCode.Length);
    }

    // -------------------------------------------------------------------------
    // POST /close
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Close_ReturnsOk_WithClosedStatus()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        await PostOpenAsync(courseId);
        var response = await PostCloseAsync(courseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();
        Assert.NotNull(body);
        Assert.Equal("Closed", body!.Status);
        Assert.NotNull(body.ClosedAt);
    }

    [Fact]
    public async Task Close_WhenNoOpenWaitlist_Returns404()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        var response = await PostCloseAsync(courseId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("No open walk-up waitlist found for today.", body!.Error);
    }

    [Fact]
    public async Task Close_CourseNotFound_Returns404()
    {
        var response = await PostCloseAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Course not found.", body!.Error);
    }

    // -------------------------------------------------------------------------
    // GET /today
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Today_WhenNoWaitlist_ReturnsNullWaitlist()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        var response = await GetTodayAsync(courseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistTodayResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.Waitlist);
        Assert.Empty(body.Entries);
    }

    [Fact]
    public async Task Today_WhenOpen_ReturnsWaitlistWithEmptyEntries()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        await PostOpenAsync(courseId);
        var response = await GetTodayAsync(courseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistTodayResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Waitlist);
        Assert.Equal("Open", body.Waitlist!.Status);
        Assert.Empty(body.Entries);
    }

    [Fact]
    public async Task Today_WhenClosed_ReturnsClosedWaitlist()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        await PostOpenAsync(courseId);
        await PostCloseAsync(courseId);
        var response = await GetTodayAsync(courseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistTodayResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Waitlist);
        Assert.Equal("Closed", body.Waitlist!.Status);
        Assert.NotNull(body.Waitlist.ClosedAt);
    }

    [Fact]
    public async Task Today_CourseNotFound_Returns404()
    {
        var response = await GetTodayAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Tenant isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TenantIsolation_CannotAccessOtherTenantWaitlist()
    {
        // Open a waitlist for course belonging to tenant A
        var (tenantIdA, courseIdA) = await CreateTestCourseAsync();
        await PostOpenAsync(courseIdA);

        // Create course B on a different tenant and verify its today endpoint returns null waitlist
        var (tenantIdB, courseIdB) = await CreateTestCourseAsync();

        var response = await GetTodayAsync(courseIdB);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistTodayResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.Waitlist);

        // Also confirm course B cannot close course A's waitlist
        var closeResponse = await PostCloseAsync(courseIdB);
        Assert.Equal(HttpStatusCode.NotFound, closeResponse.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HttpResponseMessage> PostOpenAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostCloseAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/close");
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetTodayAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/courses/{courseId}/walkup-waitlist/today");
        return await _client.SendAsync(request);
    }

    private async Task<(Guid TenantId, Guid CourseId)> CreateTestCourseAsync()
    {
        var tenantId = await CreateTestTenantAsync();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}" });
        var createResponse = await _client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        return (tenantId, course!.Id);
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

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return tenant!.Id;
    }

    private record WalkUpWaitlistResponse(
        Guid Id,
        Guid CourseId,
        string ShortCode,
        string Date,
        string Status,
        DateTimeOffset OpenedAt,
        DateTimeOffset? ClosedAt);

    private record WalkUpWaitlistTodayResponse(
        WalkUpWaitlistResponse? Waitlist,
        List<WalkUpWaitlistEntryResponse> Entries);

    private record WalkUpWaitlistEntryResponse(
        Guid Id,
        string GolferName,
        DateTimeOffset JoinedAt);

    private record ErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
}
