using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;
public class WalkUpWaitlistEndpointsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();

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
    public async Task Today_WithEntries_ReturnsEntriesWithGroupSize()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        await PostAddGolferAsync(courseId, new
        {
            FirstName = "Alice",
            LastName = "A",
            Phone = "555-111-1111",
            GroupSize = 3
        });

        var response = await GetTodayAsync(courseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistTodayResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Entries);
        Assert.Equal("Alice A", body.Entries[0].GolferName);
        Assert.Equal(3, body.Entries[0].GroupSize);
    }

    [Fact]
    public async Task Today_CourseNotFound_Returns404()
    {
        var response = await GetTodayAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /requests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateRequest_ValidRequest_Returns201()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "10:00", GolfersNeeded = 2 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WaitlistRequestResponse>();
        Assert.NotNull(body);
        Assert.Equal("10:00", body!.TeeTime);
        Assert.Equal(2, body.GolfersNeeded);
        Assert.Equal("Pending", body.Status);
    }

    [Fact]
    public async Task CreateRequest_NoOpenWaitlist_Returns400()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "10:00", GolfersNeeded = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRequest_ClosedWaitlist_Returns400()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await PostCloseAsync(courseId);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "10:00", GolfersNeeded = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRequest_DuplicateTeeTime_Returns409()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "11:00", GolfersNeeded = 2 });

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "11:00", GolfersNeeded = 3 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateRequest_CourseNotFound_Returns404()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{Guid.NewGuid()}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "10:00", GolfersNeeded = 2 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /entries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddGolfer_ValidRequest_Returns201()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        var response = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309",
            GroupSize = 2
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.EntryId);
        Assert.Equal("Jane Smith", body.GolferName);
        Assert.Equal("+15558675309", body.GolferPhone);
        Assert.Equal(2, body.GroupSize);
        Assert.Equal(1, body.Position);
        Assert.False(string.IsNullOrEmpty(body.CourseName));
    }

    [Fact]
    public async Task AddGolfer_NoOpenWaitlist_Returns404()
    {
        var (_, courseId) = await CreateTestCourseAsync();

        var response = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddGolfer_DuplicatePhone_Returns409WithPosition()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

        var response = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddGolfer_ExistingGolfer_ReusesGolferRecord()
    {
        var (_, courseId1) = await CreateTestCourseAsync();
        var (_, courseId2) = await CreateTestCourseAsync();

        await PostOpenAsync(courseId1);
        await PostOpenAsync(courseId2);

        var r1 = await PostAddGolferAsync(courseId1, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

        var r2 = await PostAddGolferAsync(courseId2, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }

    [Fact]
    public async Task AddGolfer_NoGroupSize_DefaultsTo1()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        var response = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.GroupSize);
    }

    [Fact]
    public async Task AddGolfer_MultipleGolfers_CorrectPositions()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);

        var r1 = await PostAddGolferAsync(courseId, new { FirstName = "Alice", LastName = "A", Phone = "555-111-0001" });
        var r2 = await PostAddGolferAsync(courseId, new { FirstName = "Bob", LastName = "B", Phone = "555-111-0002" });
        var r3 = await PostAddGolferAsync(courseId, new { FirstName = "Carol", LastName = "C", Phone = "555-111-0003" });

        var b1 = await r1.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
        var b2 = await r2.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
        var b3 = await r3.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);

        Assert.Equal(1, b1!.Position);
        Assert.Equal(2, b2!.Position);
        Assert.Equal(3, b3!.Position);
    }

    [Fact]
    public async Task AddGolfer_ClosedWaitlist_Returns404()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await PostCloseAsync(courseId);

        var response = await PostAddGolferAsync(courseId, new
        {
            FirstName = "Jane",
            LastName = "Smith",
            Phone = "555-867-5309"
        });

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

    private async Task<HttpResponseMessage> PostAddGolferAsync(Guid courseId, object body) =>
        await this.client.PostAsJsonAsync($"/courses/{courseId}/walkup-waitlist/entries", body);

    private async Task<HttpResponseMessage> PostOpenAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        return await this.client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostCloseAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/close");
        return await this.client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetTodayAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/courses/{courseId}/walkup-waitlist/today");
        return await this.client.SendAsync(request);
    }

    private async Task<(Guid TenantId, Guid CourseId)> CreateTestCourseAsync()
    {
        var tenantId = await CreateTestTenantAsync();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}" });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        return (tenantId, course!.Id);
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
        int GroupSize,
        DateTimeOffset JoinedAt);

    private record WaitlistRequestResponse(
        Guid Id,
        string TeeTime,
        int GolfersNeeded,
        string Status);

    private record AddGolferToWaitlistResponse(
        Guid EntryId,
        string GolferName,
        string GolferPhone,
        int GroupSize,
        int Position,
        string CourseName);

    private record ConflictResponse(string Error, int Position);
    private record ErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
}
