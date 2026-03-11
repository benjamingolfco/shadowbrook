using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests;

public class SmsOfferIntegrationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private InMemoryTextMessageService SmsService => factory.Services.GetRequiredService<InMemoryTextMessageService>();

    // -------------------------------------------------------------------------
    // AC1: Receiving the Offer — SMS sent when golfer is in queue
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateRequest_WithGolferInQueue_SendsSmsOffer()
    {
        SmsService.Clear();
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await AddGolferAsync(courseId, "Jane", "Smith", "+15558675309");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "09:20", GolfersNeeded = 1 });

        var messages = SmsService.GetByPhone("+15558675309");
        var offer = messages.FirstOrDefault(m => m.Direction == SmsDirection.Outbound && m.Body.Contains("9:20 AM"));
        Assert.NotNull(offer);
        Assert.Contains("Reply Y to claim or N to pass", offer!.Body);
        Assert.Contains("5 min", offer.Body);
    }

    // -------------------------------------------------------------------------
    // AC4: No One Left in Queue — no SMS sent
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateRequest_EmptyQueue_NoSmsSent()
    {
        SmsService.Clear();
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        // No golfers added

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "10:00", GolfersNeeded = 2 });

        var allMessages = SmsService.GetAll();
        Assert.Empty(allMessages);
    }

    // -------------------------------------------------------------------------
    // AC2: Claiming the Slot — reply "Y" confirms booking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_ReplyY_ClaimsSlot()
    {
        SmsService.Clear();
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await AddGolferAsync(courseId, "Alice", "A", "+15550000001");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "11:00", GolfersNeeded = 1 });

        var webhookResponse = await PostInboundSmsAsync("+15550000001", "Y");
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        // Confirmation SMS sent
        var messages = SmsService.GetByPhone("+15550000001");
        var confirmation = messages.FirstOrDefault(m => m.Direction == SmsDirection.Outbound && m.Body.Contains("Confirmed"));
        Assert.NotNull(confirmation);

        // Golfer removed from waitlist
        var todayData = await GetTodayAsync(courseId);
        Assert.Empty(todayData.Entries);
    }

    // -------------------------------------------------------------------------
    // AC3: Declining the Offer — reply "N" removes from queue
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_ReplyN_DeclinesAndRemoves()
    {
        SmsService.Clear();
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await AddGolferAsync(courseId, "Bob", "B", "+15550000002");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "12:00", GolfersNeeded = 1 });

        var webhookResponse = await PostInboundSmsAsync("+15550000002", "N");
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        // Removal confirmation sent
        var messages = SmsService.GetByPhone("+15550000002");
        var removal = messages.FirstOrDefault(m => m.Direction == SmsDirection.Outbound && m.Body.Contains("removed from the waitlist"));
        Assert.NotNull(removal);

        // Golfer no longer in waitlist
        var todayData = await GetTodayAsync(courseId);
        Assert.Empty(todayData.Entries);
    }

    // -------------------------------------------------------------------------
    // Edge case: Expired offer — reply "Y" after window closes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_ReplyY_AfterExpiry_RejectsGracefully()
    {
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TimeProvider));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<TimeProvider>(fakeTime);
            });
        });
        var customClient = customFactory.CreateClient();
        var customSms = customFactory.Services.GetRequiredService<InMemoryTextMessageService>();
        customSms.Clear();

        var (_, courseId) = await CreateTestCourseAsync(customClient);
        await PostOpenAsync(courseId, customClient);
        await AddGolferAsync(courseId, "Carol", "C", "+15550000003", customClient);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await customClient.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "13:00", GolfersNeeded = 1 });

        // Advance time past the 5-minute response window
        fakeTime.Advance(TimeSpan.FromMinutes(6));

        var webhookResponse = await customClient.PostAsJsonAsync("/webhooks/sms/inbound",
            new { From = "+15550000003", Body = "Y" });
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        // Window-closed SMS sent
        var messages = customSms.GetByPhone("+15550000003");
        var rejection = messages.FirstOrDefault(m =>
            m.Direction == SmsDirection.Outbound && m.Body.Contains("response window"));
        Assert.NotNull(rejection);
    }

    // -------------------------------------------------------------------------
    // Edge case: Unknown phone returns 200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_UnknownPhone_Returns200()
    {
        var response = await PostInboundSmsAsync("+15559999999", "Y");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Edge case: Garbage body sends help SMS
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_GarbageBody_SendsHelpSms()
    {
        SmsService.Clear();
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await AddGolferAsync(courseId, "Frank", "F", "+15550000006");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "15:00", GolfersNeeded = 1 });

        var response = await PostInboundSmsAsync("+15550000006", "HELLO");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var messages = SmsService.GetByPhone("+15550000006");
        var helpMsg = messages.FirstOrDefault(m =>
            m.Direction == SmsDirection.Outbound && m.Body.Contains("Reply Y to claim the tee time or N to pass"));
        Assert.NotNull(helpMsg);
    }

    // -------------------------------------------------------------------------
    // Integration: Booking visible on tee sheet after claim
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_ReplyY_CreatesBooking_VisibleOnTeeSheet()
    {
        SmsService.Clear();
        var (tenantId, courseId) = await CreateTestCourseAsync();

        // Configure tee time settings so the tee sheet endpoint works
        var settingsRequest = new HttpRequestMessage(HttpMethod.Put, $"/courses/{courseId}/tee-time-settings");
        settingsRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        settingsRequest.Content = JsonContent.Create(new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "08:00",
            LastTeeTime = "18:00"
        });
        await this.client.SendAsync(settingsRequest);

        await PostOpenAsync(courseId);
        await AddGolferAsync(courseId, "Grace", "G", "+15550000007", groupSize: 2);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "16:00", GolfersNeeded = 2 });

        await PostInboundSmsAsync("+15550000007", "Y");

        var teeSheetResponse = await this.client.GetFromJsonAsync<TeeSheetResponse>(
            $"/tee-sheets?courseId={courseId}&date={today}");
        Assert.NotNull(teeSheetResponse);

        var bookedSlot = teeSheetResponse!.Slots.FirstOrDefault(s => s.GolferName == "Grace G");
        Assert.NotNull(bookedSlot);
        Assert.Equal("booked", bookedSlot!.Status);
        Assert.Equal(2, bookedSlot.PlayerCount);
    }

    // -------------------------------------------------------------------------
    // Integration: Golfer not in today endpoint after decline
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InboundSms_ReplyN_GolferNotInTodayEndpoint()
    {
        SmsService.Clear();
        var (_, courseId) = await CreateTestCourseAsync();
        await PostOpenAsync(courseId);
        await AddGolferAsync(courseId, "Hank", "H", "+15550000008");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/requests",
            new { Date = today, TeeTime = "17:00", GolfersNeeded = 1 });

        await PostInboundSmsAsync("+15550000008", "N");

        var todayData = await GetTodayAsync(courseId);
        Assert.DoesNotContain(todayData.Entries, e => e.GolferName == "Hank H");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HttpResponseMessage> PostInboundSmsAsync(string from, string body) =>
        await this.client.PostAsJsonAsync("/webhooks/sms/inbound", new { From = from, Body = body });

    private async Task<TodayResponse> GetTodayAsync(Guid courseId) =>
        (await this.client.GetFromJsonAsync<TodayResponse>($"/courses/{courseId}/walkup-waitlist/today"))!;

    private async Task<HttpResponseMessage> PostOpenAsync(Guid courseId, HttpClient? c = null) =>
        await (c ?? this.client).SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open"));

    private async Task<HttpResponseMessage> AddGolferAsync(
        Guid courseId, string firstName, string lastName, string phone,
        HttpClient? c = null, int groupSize = 1) =>
        await (c ?? this.client).PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/entries",
            new { FirstName = firstName, LastName = lastName, Phone = phone, GroupSize = groupSize });

    private async Task<(Guid TenantId, Guid CourseId)> CreateTestCourseAsync(HttpClient? c = null)
    {
        var httpClient = c ?? this.client;

        var tenantResponse = await httpClient.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = $"Tenant {Guid.NewGuid()}",
            ContactName = "Contact",
            ContactEmail = "contact@test.com",
            ContactPhone = "555-0000"
        });
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<IdResponse>();

        var courseRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        courseRequest.Headers.Add("X-Tenant-Id", tenant!.Id.ToString());
        courseRequest.Content = JsonContent.Create(new { Name = $"Shadowbrook {Guid.NewGuid()}" });
        var courseResponse = await httpClient.SendAsync(courseRequest);
        var course = await courseResponse.Content.ReadFromJsonAsync<IdResponse>();

        return (tenant.Id, course!.Id);
    }

    // -------------------------------------------------------------------------
    // Response records
    // -------------------------------------------------------------------------

    private record TodayResponse(WaitlistResponse? Waitlist, List<EntryResponse> Entries, List<object> Requests);
    private record WaitlistResponse(Guid Id, string Status);
    private record EntryResponse(Guid Id, string GolferName, int GroupSize, DateTimeOffset JoinedAt);
    private record TeeSheetResponse(Guid CourseId, string CourseName, string Date, List<TeeSheetSlot> Slots);
    private record TeeSheetSlot(string Time, string Status, string? GolferName, int PlayerCount);
    private record IdResponse(Guid Id);
}
