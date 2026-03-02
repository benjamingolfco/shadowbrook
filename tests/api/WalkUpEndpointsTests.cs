using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Tests;

public class WalkUpEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public WalkUpEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // POST /walkup/verify
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyCode_ValidActiveCode_ReturnsOkWithCourseName()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateWalkUpCodeAsync(courseId, "1111", today);

        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "1111" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VerifyCodeResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.CourseWaitlistId);
        Assert.Equal(courseId, body.CourseId);
        Assert.False(string.IsNullOrEmpty(body.CourseName));
        Assert.Equal(today.ToString("yyyy-MM-dd"), body.Date);
    }

    [Fact]
    public async Task VerifyCode_InvalidCode_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "9999" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_ExpiredCode_ReturnsGone()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await CreateWalkUpCodeAsync(courseId, "2222", yesterday);

        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "2222" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_InactiveCode_ReturnsGone()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateWalkUpCodeAsync(courseId, "3333", today, isActive: false);

        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "3333" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_EmptyCode_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_NonNumericCode_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "abcd" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_WrongLength_ReturnsBadRequest()
    {
        var response1 = await _client.PostAsJsonAsync("/walkup/verify", new { code = "123" });
        var response2 = await _client.PostAsJsonAsync("/walkup/verify", new { code = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_CreatesWaitlistIfNotExists()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateWalkUpCodeAsync(courseId, "4444", today);

        // No waitlist row exists yet
        var waitlistBefore = await GetCourseWaitlistAsync(courseId, today);
        Assert.Null(waitlistBefore);

        var response = await _client.PostAsJsonAsync("/walkup/verify", new { code = "4444" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Waitlist row should now exist
        var waitlistAfter = await GetCourseWaitlistAsync(courseId, today);
        Assert.NotNull(waitlistAfter);
        Assert.Equal(courseId, waitlistAfter!.CourseId);
        Assert.Equal(today, waitlistAfter.Date);
    }

    // -------------------------------------------------------------------------
    // POST /walkup/join
    // -------------------------------------------------------------------------

    [Fact]
    public async Task JoinWaitlist_ValidData_ReturnsOkWithPosition()
    {
        var waitlistId = await CreateWaitlistForToday();

        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Tiger",
            lastName = "Woods",
            phone = "(612) 555-0001"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.EntryId);
        Assert.Equal("Tiger", body.FirstName);
        Assert.Equal(1, body.Position);
        Assert.False(body.IsExisting);
    }

    [Fact]
    public async Task JoinWaitlist_SecondGolfer_ReturnsPosition2()
    {
        var waitlistId = await CreateWaitlistForToday();

        await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Tiger",
            lastName = "Woods",
            phone = "6125550010"
        });

        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Phil",
            lastName = "Mickelson",
            phone = "6125550011"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Position);
    }

    [Fact]
    public async Task JoinWaitlist_DuplicatePhone_ReturnsExistingPosition()
    {
        var waitlistId = await CreateWaitlistForToday();

        // First join
        await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Rory",
            lastName = "McIlroy",
            phone = "6125550020"
        });

        // Duplicate join with same phone
        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Rory",
            lastName = "McIlroy",
            phone = "6125550020"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsExisting);

        // Only one entry should exist in the database
        var entryCount = await CountWaitlistEntriesAsync(waitlistId, "+16125550020");
        Assert.Equal(1, entryCount);
    }

    [Fact]
    public async Task JoinWaitlist_MissingFirstName_ReturnsBadRequest()
    {
        var waitlistId = await CreateWaitlistForToday();

        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "",
            lastName = "Woods",
            phone = "6125550030"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JoinWaitlist_MissingLastName_ReturnsBadRequest()
    {
        var waitlistId = await CreateWaitlistForToday();

        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Tiger",
            lastName = "",
            phone = "6125550040"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JoinWaitlist_InvalidPhone_ReturnsBadRequest()
    {
        var waitlistId = await CreateWaitlistForToday();

        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Tiger",
            lastName = "Woods",
            phone = "abc"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JoinWaitlist_InvalidCourseWaitlistId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = Guid.NewGuid(),
            firstName = "Tiger",
            lastName = "Woods",
            phone = "6125550050"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task JoinWaitlist_PhoneNormalization_DifferentFormats_MatchSameGolfer()
    {
        var waitlistId = await CreateWaitlistForToday();

        // Join with formatted phone
        await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Jon",
            lastName = "Rahm",
            phone = "(612) 555-0060"
        });

        // Rejoin with different format of same number — should detect as duplicate
        var response = await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Jon",
            lastName = "Rahm",
            phone = "+16125550060"   // E.164 format of same number
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsExisting);
    }

    [Fact]
    public async Task JoinWaitlist_CreatesGolferRecord()
    {
        var waitlistId = await CreateWaitlistForToday();

        await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId,
            firstName = "Scottie",
            lastName = "Scheffler",
            phone = "6125550070"
        });

        var golfer = await GetGolferByPhoneAsync("+16125550070");
        Assert.NotNull(golfer);
        Assert.Equal("Scottie", golfer!.FirstName);
        Assert.Equal("Scheffler", golfer.LastName);
        Assert.Equal("+16125550070", golfer.Phone);
    }

    [Fact]
    public async Task JoinWaitlist_ExistingGolfer_ReusesRecord()
    {
        var waitlistId1 = await CreateWaitlistForToday();
        var waitlistId2 = await CreateWaitlistForToday();

        // Golfer joins first course's waitlist
        await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId1,
            firstName = "Xander",
            lastName = "Schauffele",
            phone = "6125550080"
        });

        var golferAfterFirst = await GetGolferByPhoneAsync("+16125550080");
        Assert.NotNull(golferAfterFirst);
        var golferId = golferAfterFirst!.Id;

        // Same golfer joins a different waitlist (different course)
        await _client.PostAsJsonAsync("/walkup/join", new
        {
            courseWaitlistId = waitlistId2,
            firstName = "Xander",
            lastName = "Schauffele",
            phone = "6125550080"
        });

        var golferAfterSecond = await GetGolferByPhoneAsync("+16125550080");
        Assert.NotNull(golferAfterSecond);
        // Same Golfer.Id — no duplicate record created
        Assert.Equal(golferId, golferAfterSecond!.Id);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<(Guid TenantId, Guid CourseId)> CreateTestCourseAsync()
    {
        var orgName = $"Test Tenant {Guid.NewGuid()}";
        var tenantResponse = await _client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = orgName,
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRecord>();

        var courseRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        courseRequest.Headers.Add("X-Tenant-Id", tenant!.Id.ToString());
        courseRequest.Content = JsonContent.Create(new { Name = $"Test Course {Guid.NewGuid()}" });
        var courseResponse = await _client.SendAsync(courseRequest);
        var course = await courseResponse.Content.ReadFromJsonAsync<CourseRecord>();

        return (tenant.Id, course!.Id);
    }

    private async Task CreateWalkUpCodeAsync(Guid courseId, string code, DateOnly date, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.WalkUpCodes.Add(new WalkUpCode
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Code = code,
            Date = date,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateWaitlistForToday()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var waitlist = new CourseWaitlist
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Date = today,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.CourseWaitlists.Add(waitlist);
        await db.SaveChangesAsync();

        return waitlist.Id;
    }

    private async Task<CourseWaitlist?> GetCourseWaitlistAsync(Guid courseId, DateOnly date)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.CourseWaitlists
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date);
    }

    private async Task<int> CountWaitlistEntriesAsync(Guid courseWaitlistId, string normalizedPhone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.GolferWaitlistEntries
            .CountAsync(e => e.CourseWaitlistId == courseWaitlistId && e.GolferPhone == normalizedPhone && e.RemovedAt == null);
    }

    private async Task<Golfer?> GetGolferByPhoneAsync(string normalizedPhone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Golfers.FirstOrDefaultAsync(g => g.Phone == normalizedPhone);
    }

    // -------------------------------------------------------------------------
    // Response records (local to test class, mirroring API responses)
    // -------------------------------------------------------------------------

    private record TenantRecord(Guid Id);
    private record CourseRecord(Guid Id, string Name);

    private record VerifyCodeResponse(
        Guid CourseWaitlistId,
        Guid CourseId,
        string CourseName,
        string Date);

    private record JoinWaitlistResponse(
        Guid EntryId,
        string FirstName,
        int Position,
        bool IsExisting);
}
