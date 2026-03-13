using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests;

public class WaitlistOfferEndpointsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private readonly InMemoryTextMessageService smsService = factory.Services.GetRequiredService<InMemoryTextMessageService>();

    // -------------------------------------------------------------------------
    // GET /waitlist/offers/{token}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ViewOffer_ValidToken_Returns200WithOfferDetails()
    {
        var token = await CreateOfferAndGetTokenAsync();

        var response = await this.client.GetAsync($"/waitlist/offers/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WaitlistOfferResponse>();
        Assert.NotNull(body);
        Assert.Equal(token, body!.Token);
        Assert.NotNull(body.CourseName);
        Assert.NotNull(body.Date);
        Assert.NotNull(body.TeeTime);
        Assert.Equal("Pending", body.Status);
        Assert.True(body.GolfersNeeded > 0);
        Assert.NotNull(body.GolferName);
    }

    [Fact]
    public async Task ViewOffer_InvalidToken_Returns404()
    {
        var response = await this.client.GetAsync($"/waitlist/offers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Offer not found.", body!.Error);
    }

    [Fact]
    public async Task ViewOffer_ExpiredOffer_ReturnsExpiredStatus()
    {
        // Create an offer with past expiration
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");

        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);

        // Manually update the offer to be expired in the database
        var token = await GetOfferTokenFromSmsAsync(phone);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();
        var offer = await db.WaitlistOffers.FirstOrDefaultAsync(o => o.Token == token);
        Assert.NotNull(offer);
        offer!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var response = await this.client.GetAsync($"/waitlist/offers/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WaitlistOfferResponse>();
        Assert.NotNull(body);
        Assert.Equal("Expired", body!.Status);
    }

    // -------------------------------------------------------------------------
    // POST /waitlist/offers/{token}/accept
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AcceptOffer_ValidPendingOffer_Returns200()
    {
        var token = await CreateOfferAndGetTokenAsync();

        var response = await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WaitlistOfferAcceptResponse>();
        Assert.NotNull(body);
        Assert.Equal("Accepted", body!.Status);
        Assert.Equal("You're booked!", body.Message);
        Assert.NotNull(body.CourseName);
    }

    [Fact]
    public async Task AcceptOffer_CreatesBooking()
    {
        var token = await CreateOfferAndGetTokenAsync();

        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        // Verify booking was created
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var bookingExists = await db.Bookings.AnyAsync(b => b.GolferName.Contains("Jane") && b.PlayerCount == 1);
        Assert.True(bookingExists, "Booking should be created after accepting offer");
    }

    [Fact]
    public async Task AcceptOffer_RemovesGolferFromWaitlist()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);
        var token = await GetOfferTokenFromSmsAsync(phone);

        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        // Verify golfer was soft-deleted from waitlist
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var entry = await db.GolferWaitlistEntries.FirstOrDefaultAsync(e => e.GolferPhone == phone);
        Assert.NotNull(entry);
        Assert.NotNull(entry!.RemovedAt);
    }

    [Fact]
    public async Task AcceptOffer_SendsConfirmationSms()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);
        var token = await GetOfferTokenFromSmsAsync(phone);

        this.smsService.Clear();
        var response = await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var messages = this.smsService.GetAll();
        var confirmationSms = messages.FirstOrDefault(m => m.Body.Contains("You're booked!") && m.To == phone);
        Assert.NotNull(confirmationSms);
    }

    [Fact]
    public async Task AcceptOffer_ExpiredOffer_Returns409()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);
        var token = await GetOfferTokenFromSmsAsync(phone);

        // Expire the offer
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();
        var offer = await db.WaitlistOffers.FirstOrDefaultAsync(o => o.Token == token);
        Assert.NotNull(offer);
        offer!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var response = await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("This offer has expired.", body!.Error);
    }

    [Fact]
    public async Task AcceptOffer_AlreadyAcceptedOffer_Returns409()
    {
        var token = await CreateOfferAndGetTokenAsync();

        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);
        var response = await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("This offer is no longer available.", body!.Error);
    }

    [Fact]
    public async Task AcceptOffer_InvalidToken_Returns404()
    {
        var response = await this.client.PostAsync($"/waitlist/offers/{Guid.NewGuid()}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AcceptOffer_AllSlotsFilled_Returns409()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);

        var phone1 = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        var phone2 = await AddGolferToWaitlistAsync(courseId, "John", "Doe", "555-111-2222");

        await CreateTeeTimeRequestAsync(courseId, "10:00", 1); // Only 1 slot

        var token1 = await GetOfferTokenFromSmsAsync(phone1);
        var token2 = await GetOfferTokenFromSmsAsync(phone2);

        // First golfer accepts
        var response1 = await this.client.PostAsync($"/waitlist/offers/{token1}/accept", null);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Second golfer tries to accept - the offer was expired by the system when all slots were filled
        var response2 = await this.client.PostAsync($"/waitlist/offers/{token2}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

        var body = await response2.Content.ReadFromJsonAsync<ErrorResponse>();
        // Either message is valid: offer was expired when slot was filled OR we check slot count
        Assert.True(
            body!.Error == "All slots have been filled." || body.Error == "This offer is no longer available.",
            $"Expected error about slots filled or offer unavailable, got: {body.Error}");
    }

    // -------------------------------------------------------------------------
    // SMS notification tests (triggered by TeeTimeRequestAdded)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateRequest_WithEligibleGolfers_SendsOfferSms()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");

        this.smsService.Clear();
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);

        var messages = this.smsService.GetAll();
        var offerSms = messages.FirstOrDefault(m => m.To == phone && m.Body.Contains("tee time just opened"));
        Assert.NotNull(offerSms);
        Assert.Contains("Claim your spot:", offerSms!.Body);
        Assert.Contains("You have 15 minutes", offerSms.Body);
    }

    [Fact]
    public async Task CreateRequest_NoEligibleGolfers_NoSmsSent()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);

        this.smsService.Clear();
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);

        var messages = this.smsService.GetAll();
        Assert.Empty(messages);
    }

    [Fact]
    public async Task CreateRequest_WithEligibleGolfers_CreatesWaitlistOffers()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone1 = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        var phone2 = await AddGolferToWaitlistAsync(courseId, "John", "Doe", "555-111-2222");

        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var offers = await db.WaitlistOffers
            .Where(o => o.CourseId == courseId)
            .ToListAsync();
        Assert.Equal(2, offers.Count);
        Assert.Contains(offers, o => o.GolferPhone == phone1);
        Assert.Contains(offers, o => o.GolferPhone == phone2);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<Guid> CreateOfferAndGetTokenAsync()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);
        return await GetOfferTokenFromSmsAsync(phone);
    }

    private async Task<Guid> GetOfferTokenFromSmsAsync(string phone)
    {
        var messages = this.smsService.GetByPhone(phone);
        // Get the most recent offer message for this phone
        var offerMessage = messages.LastOrDefault(m => m.Body.Contains("Claim your spot:"));
        Assert.NotNull(offerMessage);

        // Extract token from URL in message body
        var urlStart = offerMessage!.Body.IndexOf("/book/walkup/") + "/book/walkup/".Length;
        var urlEnd = offerMessage.Body.IndexOf(" ", urlStart);
        if (urlEnd == -1)
        {
            urlEnd = offerMessage.Body.Length;
        }

        var tokenString = offerMessage.Body.Substring(urlStart, urlEnd - urlStart);
        return Guid.Parse(tokenString);
    }

    private async Task<string> AddGolferToWaitlistAsync(Guid courseId, string firstName, string lastName, string phone)
    {
        var normalized = Shadowbrook.Api.Infrastructure.Services.PhoneNormalizer.Normalize(phone);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/entries");
        var tenantId = await GetTenantIdForCourseAsync(courseId);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new { FirstName = firstName, LastName = lastName, Phone = phone });
        await this.client.SendAsync(request);

        return normalized!;
    }

    private async Task CreateTeeTimeRequestAsync(Guid courseId, string teeTime, int golfersNeeded)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/requests");
        var tenantId = await GetTenantIdForCourseAsync(courseId);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new { Date = today, TeeTime = teeTime, GolfersNeeded = golfersNeeded });
        await this.client.SendAsync(request);
    }

    private async Task OpenWaitlistAsync(Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        var tenantId = await GetTenantIdForCourseAsync(courseId);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        await this.client.SendAsync(request);
    }

    private async Task<Guid> GetTenantIdForCourseAsync(Guid courseId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();
        var course = await db.Courses.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == courseId);
        return course!.TenantId;
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

    private record WaitlistOfferResponse(
        Guid Token,
        string CourseName,
        string Date,
        string TeeTime,
        int GolfersNeeded,
        string GolferName,
        string Status,
        DateTimeOffset ExpiresAt);

    private record WaitlistOfferAcceptResponse(
        string Status,
        string CourseName,
        string Date,
        string TeeTime,
        string GolferName,
        string Message);

    private record ErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
}
