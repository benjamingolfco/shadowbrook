using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;

public class WalkupJoinEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WalkupJoinEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // POST /walkup/verify
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Verify_ValidActiveCode_ReturnsCourseName()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();

        var response = await PostVerifyAsync(shortCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VerifyCodeResponse>();
        Assert.NotNull(body);
        Assert.Equal(shortCode, body!.ShortCode);
        Assert.NotNull(body.CourseName);
        Assert.NotEqual(Guid.Empty, body.CourseWaitlistId);
    }

    [Fact]
    public async Task Verify_InvalidCode_Returns404()
    {
        var response = await PostVerifyAsync("0000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Code not found or waitlist is not active.", body!.Error);
    }

    [Fact]
    public async Task Verify_ClosedWaitlistCode_Returns404()
    {
        var (_, courseId, shortCode) = await CreateOpenWaitlistAsync();
        await PostCloseWaitlistAsync(courseId);

        var response = await PostVerifyAsync(shortCode);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Code not found or waitlist is not active.", body!.Error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData("12ab")]
    public async Task Verify_MalformedCode_Returns400(string code)
    {
        var response = await PostVerifyAsync(code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /walkup/join
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Join_ValidRequest_Returns201_WithPosition()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "John", "Smith", "555-123-4567");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.NotNull(body);
        Assert.Equal("John Smith", body!.GolferName);
        Assert.Equal(1, body.Position);
        Assert.NotNull(body.CourseName);
        Assert.NotEqual(Guid.Empty, body.EntryId);
    }

    [Fact]
    public async Task Join_CreatesGolferRecord()
    {
        // Verify a Golfer entity is created when joining for the first time
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var phone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}";
        var joinResponse = await PostJoinAsync(verifyBody!.CourseWaitlistId, "Alice", "Wonder", phone);

        Assert.Equal(HttpStatusCode.Created, joinResponse.StatusCode);

        // Confirm golfer was created by joining a second waitlist with same phone -- should not duplicate
        var (_, _, shortCode2) = await CreateOpenWaitlistAsync();
        var verifyBody2 = await (await PostVerifyAsync(shortCode2)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var joinResponse2 = await PostJoinAsync(verifyBody2!.CourseWaitlistId, "Alice", "Wonder", phone);

        Assert.Equal(HttpStatusCode.Created, joinResponse2.StatusCode);
    }

    [Fact]
    public async Task Join_ExistingGolfer_ReusesGolferRecord()
    {
        // Same phone, different waitlist -- should succeed (no 409, golfer is reused)
        var (_, _, shortCode1) = await CreateOpenWaitlistAsync();
        var verifyBody1 = await (await PostVerifyAsync(shortCode1)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var phone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}";
        var join1 = await PostJoinAsync(verifyBody1!.CourseWaitlistId, "Bob", "Golfer", phone);
        Assert.Equal(HttpStatusCode.Created, join1.StatusCode);

        var (_, _, shortCode2) = await CreateOpenWaitlistAsync();
        var verifyBody2 = await (await PostVerifyAsync(shortCode2)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var join2 = await PostJoinAsync(verifyBody2!.CourseWaitlistId, "Bob", "Golfer", phone);
        Assert.Equal(HttpStatusCode.Created, join2.StatusCode);

        var body2 = await join2.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal("Bob Golfer", body2!.GolferName);
    }

    [Fact]
    public async Task Join_DuplicatePhone_Returns409_WithPosition()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var phone = "555-111-2222";
        await PostJoinAsync(verifyBody!.CourseWaitlistId, "Chris", "Dup", phone);

        // Same phone, same waitlist
        var response = await PostJoinAsync(verifyBody.CourseWaitlistId, "Chris", "Dup", phone);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DuplicateEntryError>();
        Assert.Equal("You're already on the waitlist.", body!.Error);
        Assert.Equal(1, body.Position);
    }

    [Fact]
    public async Task Join_InvalidPhone_Returns400()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "Jane", "Doe", "123");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_MissingFirstName_Returns400()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "", "Smith", "555-123-4567");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_MissingLastName_Returns400()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "John", "", "555-123-4567");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_ClosedWaitlist_Returns404()
    {
        var (_, courseId, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        // Close the waitlist between verify and join
        await PostCloseWaitlistAsync(courseId);

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "John", "Smith", "555-999-8888");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Join_NonexistentWaitlist_Returns404()
    {
        var response = await PostJoinAsync(Guid.NewGuid(), "John", "Smith", "555-123-4567");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Join_MultipleGolfers_PositionsAreCorrect()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var waitlistId = verifyBody!.CourseWaitlistId;

        var r1 = await PostJoinAsync(waitlistId, "Alice", "One", "555-001-0001");
        var r2 = await PostJoinAsync(waitlistId, "Bob", "Two", "555-002-0002");
        var r3 = await PostJoinAsync(waitlistId, "Carol", "Three", "555-003-0003");

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);

        var b1 = await r1.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        var b2 = await r2.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        var b3 = await r3.Content.ReadFromJsonAsync<JoinWaitlistResponse>();

        Assert.Equal(1, b1!.Position);
        Assert.Equal(2, b2!.Position);
        Assert.Equal(3, b3!.Position);
    }

    [Fact]
    public async Task Join_SendsSmsConfirmation_DoesNotBreakFlow()
    {
        // SMS sending should not break the main flow (ConsoleTextMessageService is used in tests)
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "SMS", "Tester", "555-777-8888");

        // If SMS handler fires and fails it's logged but does not fail the request
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HttpResponseMessage> PostVerifyAsync(string code)
    {
        return await _client.PostAsJsonAsync("/walkup/verify", new { Code = code });
    }

    private async Task<HttpResponseMessage> PostJoinAsync(
        Guid courseWaitlistId,
        string firstName,
        string lastName,
        string phone)
    {
        return await _client.PostAsJsonAsync("/walkup/join", new
        {
            CourseWaitlistId = courseWaitlistId,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone
        });
    }

    private async Task<HttpResponseMessage> PostOpenWaitlistAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostCloseWaitlistAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/close");
        return await _client.SendAsync(request);
    }

    private async Task<(Guid TenantId, Guid CourseId, string ShortCode)> CreateOpenWaitlistAsync()
    {
        var tenantId = await CreateTestTenantAsync();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}" });
        var createResponse = await _client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        var openResponse = await PostOpenWaitlistAsync(course!.Id);
        var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        return (tenantId, course.Id, waitlist!.ShortCode);
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

    private record VerifyCodeResponse(Guid CourseWaitlistId, string CourseName, string ShortCode);
    private record JoinWaitlistResponse(Guid EntryId, string GolferName, int Position, string CourseName);
    private record DuplicateEntryError(string Error, int Position);
    private record ErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
    private record WalkUpWaitlistResponse(Guid Id, string ShortCode);
}
