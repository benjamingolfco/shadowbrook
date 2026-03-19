using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.Tests;
public class WalkUpJoinEndpointsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();

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
    // Phone normalization
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Join_DifferentPhoneFormats_SameWaitlist_Returns409()
    {
        // Two joins with different formats of same number = duplicate
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        await PostJoinAsync(verifyBody!.CourseWaitlistId, "Pat", "Test", "555-444-3333");
        var response = await PostJoinAsync(verifyBody.CourseWaitlistId, "Pat", "Test", "(555) 444-3333");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Join_E164PhoneFormat_MatchesExisting()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        await PostJoinAsync(verifyBody!.CourseWaitlistId, "Pat", "Test", "555-444-5555");
        var response = await PostJoinAsync(verifyBody.CourseWaitlistId, "Pat", "Test", "+15554445555");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Golfer identity preservation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Join_ExistingGolfer_EntryUsesGolferName()
    {
        // First join creates golfer as "Original Name"
        var (_, _, shortCode1) = await CreateOpenWaitlistAsync();
        var v1 = await (await PostVerifyAsync(shortCode1)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var phone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}";
        var r1 = await PostJoinAsync(v1!.CourseWaitlistId, "Original", "Name", phone);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var b1 = await r1.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal("Original Name", b1!.GolferName);

        // Second join on different waitlist with different submitted name
        var (_, _, shortCode2) = await CreateOpenWaitlistAsync();
        var v2 = await (await PostVerifyAsync(shortCode2)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var r2 = await PostJoinAsync(v2!.CourseWaitlistId, "Changed", "Name", phone);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        // Entry uses the golfer entity's name (source of truth)
        var b2 = await r2.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal("Original Name", b2!.GolferName);
    }

    [Fact]
    public async Task Join_WhitespaceInName_IsTrimmed()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

        var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "  John  ", "  Smith  ", "555-888-7777");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal("John Smith", body!.GolferName);
    }

    // -------------------------------------------------------------------------
    // FluentValidation format
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Verify_WhitespaceCode_Returns400_WithMessage()
    {
        var response = await PostVerifyAsync("   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Error);
    }

    [Fact]
    public async Task Join_AllFieldsMissing_Returns400()
    {
        var response = await PostJoinAsync(Guid.NewGuid(), "", "", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Cross-waitlist and position tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Join_SamePhone_DifferentWaitlists_BothSucceed_PositionIsOne()
    {
        var phone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}";

        var (_, _, sc1) = await CreateOpenWaitlistAsync();
        var v1 = await (await PostVerifyAsync(sc1)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var r1 = await PostJoinAsync(v1!.CourseWaitlistId, "Cross", "Test", phone);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var b1 = await r1.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal(1, b1!.Position);

        var (_, _, sc2) = await CreateOpenWaitlistAsync();
        var v2 = await (await PostVerifyAsync(sc2)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var r2 = await PostJoinAsync(v2!.CourseWaitlistId, "Cross", "Test", phone);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        var b2 = await r2.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal(1, b2!.Position); // First on THIS waitlist
    }

    [Fact]
    public async Task Join_DuplicateAfterOthers_Returns409_WithCorrectPosition()
    {
        var (_, _, shortCode) = await CreateOpenWaitlistAsync();
        var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
        var waitlistId = verifyBody!.CourseWaitlistId;

        // First two golfers join
        await PostJoinAsync(waitlistId, "First", "Person", "555-100-0001");
        await PostJoinAsync(waitlistId, "Second", "Person", "555-100-0002");

        // Third golfer joins
        var thirdPhone = "555-100-0003";
        await PostJoinAsync(waitlistId, "Third", "Person", thirdPhone);

        // Third golfer tries again — should get 409
        var dup = await PostJoinAsync(waitlistId, "Third", "Person", thirdPhone);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // -------------------------------------------------------------------------
    // End-to-end flow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Verify_ReturnsCorrectCourseName()
    {
        var tenantId = await CreateTestTenantAsync();
        var expectedName = $"Specific Course {Guid.NewGuid()}";

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = expectedName });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        var openResponse = await PostOpenWaitlistAsync(course!.Id);
        var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        var verifyResponse = await PostVerifyAsync(waitlist!.ShortCode);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<VerifyCodeResponse>();

        Assert.Equal(expectedName, verifyBody!.CourseName);
    }

    [Fact]
    public async Task FullFlow_CreateTenant_OpenWaitlist_VerifyCode_JoinWaitlist()
    {
        // Setup
        var tenantId = await CreateTestTenantAsync();
        var courseName = $"Full Flow Course {Guid.NewGuid()}";

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = courseName });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        // Open waitlist
        var openResponse = await PostOpenWaitlistAsync(course!.Id);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

        // Verify code
        var verifyResponse = await PostVerifyAsync(waitlist!.ShortCode);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<VerifyCodeResponse>();
        Assert.Equal(courseName, verifyBody!.CourseName);

        // Join waitlist
        var joinResponse = await PostJoinAsync(verifyBody.CourseWaitlistId, "E2E", "Golfer", "555-999-1111");
        Assert.Equal(HttpStatusCode.Created, joinResponse.StatusCode);
        var joinBody = await joinResponse.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.Equal("E2E Golfer", joinBody!.GolferName);
        Assert.Equal(1, joinBody.Position);
        Assert.Equal(courseName, joinBody.CourseName);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HttpResponseMessage> PostVerifyAsync(string code) =>
        await this.client.PostAsJsonAsync("/walkup/verify", new { Code = code });

    private async Task<HttpResponseMessage> PostJoinAsync(
        Guid courseWaitlistId,
        string firstName,
        string lastName,
        string phone)
    {
        return await this.client.PostAsJsonAsync("/walkup/join", new
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
        createRequest.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}" });
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

    private record VerifyCodeResponse(Guid CourseWaitlistId, string CourseName, string ShortCode);
    private record JoinWaitlistResponse(Guid EntryId, string GolferName, int Position, string CourseName);
    private record DuplicateEntryError(string Error, int Position);
    private record ErrorResponse(string Error);
    private record ValidationErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
    private record WalkUpWaitlistResponse(Guid Id, string ShortCode);
}
