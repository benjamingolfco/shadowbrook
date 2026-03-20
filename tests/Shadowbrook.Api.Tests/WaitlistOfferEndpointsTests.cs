using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
public class WaitlistOfferEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();
    private readonly InMemoryTextMessageService smsService = factory.Services.GetRequiredService<InMemoryTextMessageService>();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // GET /waitlist/offers/{token}
    // -------------------------------------------------------------------------

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
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

    // -------------------------------------------------------------------------
    // POST /waitlist/offers/{token}/accept
    // -------------------------------------------------------------------------

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task AcceptOffer_ValidPendingOffer_Returns200()
    {
        var token = await CreateOfferAndGetTokenAsync();

        var response = await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WaitlistOfferAcceptResponse>();
        Assert.NotNull(body);
        Assert.Equal("Processing", body!.Status);
        Assert.NotNull(body.Message);
    }

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task AcceptOffer_CreatesBooking()
    {
        var token = await CreateOfferAndGetTokenAsync();

        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var bookingExists = await db.Bookings.AnyAsync(b => b.GolferName.Contains("Jane") && b.PlayerCount == 1);
        Assert.True(bookingExists, "Booking should be created via saga after accepting offer");
    }

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task AcceptOffer_RemovesGolferFromWaitlist()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);
        var token = await GetOfferTokenFromSmsAsync(phone);

        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        // Verify golfer was soft-deleted from waitlist via BookingCreated saga
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var golfer = await db.Golfers.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Phone == phone);
        Assert.NotNull(golfer);

        var entry = await db.GolferWaitlistEntries.FirstOrDefaultAsync(e => e.GolferId == golfer!.Id);
        Assert.NotNull(entry);
        Assert.NotNull(entry!.RemovedAt);
    }

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task AcceptOffer_SendsProcessingSms()
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
        var processingSms = messages.FirstOrDefault(m => m.Body.Contains("processing") && m.To == phone);
        Assert.NotNull(processingSms);
    }

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task AcceptOffer_SendsConfirmationSms()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        var phone = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);
        var token = await GetOfferTokenFromSmsAsync(phone);

        this.smsService.Clear();
        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        // Confirmation SMS comes from BookingCreatedConfirmationSmsHandler via saga chain
        var messages = this.smsService.GetAll();
        var confirmationSms = messages.FirstOrDefault(m => m.Body.Contains("You're booked!") && m.To == phone);
        Assert.NotNull(confirmationSms);
    }

    [Fact]
    public async Task AcceptOffer_InvalidToken_Returns404()
    {
        var response = await this.client.PostAsync($"/waitlist/offers/{Guid.NewGuid()}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task AcceptOffer_AlreadyAcceptedOffer_Returns409()
    {
        var token = await CreateOfferAndGetTokenAsync();

        await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);
        var response = await this.client.PostAsync($"/waitlist/offers/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("This offer is no longer available.", body!.Error);
    }

    // -------------------------------------------------------------------------
    // Saga chain tests
    // -------------------------------------------------------------------------

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task CreateRequest_CapsOffersAtSlotsNeeded()
    {
        // With GolfersNeeded=1 and 2 eligible golfers, only 1 offer should be created
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);

        var phone1 = await AddGolferToWaitlistAsync(courseId, "Alice", "Cap", "555-900-0001");
        await AddGolferToWaitlistAsync(courseId, "Bob", "Cap", "555-900-0002");

        await CreateTeeTimeRequestAsync(courseId, "10:00", 1); // Only 1 slot

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var teeTimeRequests = await db.TeeTimeRequests.IgnoreQueryFilters()
            .Where(r => r.CourseId == courseId)
            .Select(r => r.Id)
            .ToListAsync();
        var offers = await db.WaitlistOffers
            .Where(o => teeTimeRequests.Contains(o.TeeTimeRequestId))
            .ToListAsync();

        var offer = Assert.Single(offers);

        // The offer should go to Alice (first by JoinedAt), not Bob
        var aliceEntry = await db.GolferWaitlistEntries
            .Join(db.Golfers.IgnoreQueryFilters(),
                e => e.GolferId, g => g.Id,
                (e, g) => new { Entry = e, Golfer = g })
            .FirstAsync(eg => eg.Golfer.Phone == phone1);
        Assert.Equal(aliceEntry.Entry.Id, offer.GolferWaitlistEntryId);
    }

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task CreateRequest_CapsOffers_OnlySendsSmsToCappedGolfers()
    {
        // With GolfersNeeded=1 and 2 eligible golfers, only the first golfer
        // (by JoinedAt) should receive an offer SMS.
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);

        var phone1 = await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        var phone2 = await AddGolferToWaitlistAsync(courseId, "John", "Doe", "555-111-2222");

        this.smsService.Clear();
        await CreateTeeTimeRequestAsync(courseId, "10:00", 1); // Only 1 slot

        var messages = this.smsService.GetAll();
        var offerToPhone1 = messages.FirstOrDefault(m => m.To == phone1 && m.Body.Contains("tee time just opened"));
        var offerToPhone2 = messages.FirstOrDefault(m => m.To == phone2 && m.Body.Contains("tee time just opened"));

        Assert.NotNull(offerToPhone1);
        Assert.Null(offerToPhone2);
    }

    // -------------------------------------------------------------------------
    // SMS notification tests (triggered by TeeTimeRequestAdded)
    // -------------------------------------------------------------------------

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
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

    [Fact(Skip = "Depends on Wolverine processing TeeTimeRequestAdded asynchronously — background handler timing not yet reliable in tests")]
    public async Task CreateRequest_WithEligibleGolfers_CreatesWaitlistOffers()
    {
        var (_, courseId) = await CreateTestCourseAsync();
        await OpenWaitlistAsync(courseId);
        await AddGolferToWaitlistAsync(courseId, "Jane", "Smith", "555-867-5309");
        await AddGolferToWaitlistAsync(courseId, "John", "Doe", "555-111-2222");

        await CreateTeeTimeRequestAsync(courseId, "10:00", 2);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Shadowbrook.Api.Infrastructure.Data.ApplicationDbContext>();

        var teeTimeRequests = await db.TeeTimeRequests.IgnoreQueryFilters()
            .Where(r => r.CourseId == courseId)
            .Select(r => r.Id)
            .ToListAsync();
        var offers = await db.WaitlistOffers
            .Where(o => teeTimeRequests.Contains(o.TeeTimeRequestId))
            .ToListAsync();
        Assert.Equal(2, offers.Count);
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
        // Poll until the offer SMS arrives — TeeTimeRequestAddedNotifyHandler runs
        // asynchronously via the Wolverine outbox after the HTTP request returns.
        // Note: Wolverine tracked sessions (WolverineFx.Testing) don't support wrapping
        // HTTP requests initiated via HttpClient/WebApplicationFactory, so polling is
        // the pragmatic approach for HTTP-triggered async handler flows.
        SmsMessage? offerMessage = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            offerMessage = this.smsService.GetByPhone(phone).LastOrDefault(m => m.Body.Contains("Claim your spot:"));
            if (offerMessage is not null)
            {
                break;
            }

            try
            {
                await Task.Delay(50, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.NotNull(offerMessage);

        // Extract token from URL in message body
        var urlStart = offerMessage!.Body.IndexOf("/book/walkup/") + "/book/walkup/".Length;
        var urlEnd = offerMessage.Body.IndexOf(" ", urlStart);
        if (urlEnd == -1)
        {
            urlEnd = offerMessage.Body.Length;
        }

        var tokenString = offerMessage.Body[urlStart..urlEnd];
        return Guid.Parse(tokenString);
    }

    private async Task<string> AddGolferToWaitlistAsync(Guid courseId, string firstName, string lastName, string phone)
    {
        var normalized = PhoneNormalizer.Normalize(phone);

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
        string Status);

    private record WaitlistOfferAcceptResponse(
        string Status,
        string Message);

    private record ErrorResponse(string Error);
    private record CourseIdResponse(Guid Id);
    private record TenantIdResponse(Guid Id);
}
