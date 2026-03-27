using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Tests;

[Collection("Integration")]
[IntegrationTest]
public class TeeSheetEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => this.factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly TestWebApplicationFactory factory = factory;

    private async Task CreateBookingAsync(Guid courseId, string date, string time, string golferName, int playerCount)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = Booking.Create(
            bookingId: Guid.CreateVersion7(),
            courseId: courseId,
            golferId: Guid.Empty,
            date: DateOnly.ParseExact(date, "yyyy-MM-dd"),
            teeTime: TimeOnly.ParseExact(time, "HH:mm"),
            golferName: golferName,
            playerCount: playerCount);

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
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

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        return tenant!.Id;
    }

    [Fact]
    public async Task GetTeeSheet_WithValidCourseAndDate_ReturnsOk()
    {
        // Arrange - Create course with tee time settings
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "17:00"
        });

        // Act
        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=2026-02-07");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teeSheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        Assert.NotNull(teeSheet);
        Assert.Equal(course.Id, teeSheet!.CourseId);
        Assert.Equal("Test Course", teeSheet.CourseName);
        Assert.NotEmpty(teeSheet.Slots);
    }

    [Fact]
    public async Task GetTeeSheet_ShowsAllTimeSlotsWithBookingStatus()
    {
        // Arrange - Create course with tee time settings
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "08:00"
        });

        // Create a booking at 07:10
        await CreateBookingAsync(course.Id, "2026-02-07", "07:10", "John Doe", 4);

        // Act
        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=2026-02-07");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teeSheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        Assert.NotNull(teeSheet);

        // Expected slots: 07:00, 07:10, 07:20, 07:30, 07:40, 07:50 (stops before 08:00)
        Assert.Equal(6, teeSheet!.Slots.Count);

        // First slot should be open
        Assert.Equal(new DateTime(2026, 2, 7, 7, 0, 0), teeSheet.Slots[0].TeeTime);
        Assert.Equal("open", teeSheet.Slots[0].Status);

        // Second slot should be booked
        Assert.Equal(new DateTime(2026, 2, 7, 7, 10, 0), teeSheet.Slots[1].TeeTime);
        Assert.Equal("booked", teeSheet.Slots[1].Status);
        Assert.Equal("John Doe", teeSheet.Slots[1].GolferName);
        Assert.Equal(4, teeSheet.Slots[1].PlayerCount);
    }

    [Fact]
    public async Task GetTeeSheet_BookedSlotsShowGolferNamesAndPlayerCount()
    {
        // Arrange
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "08:00"
        });

        await CreateBookingAsync(course.Id, "2026-02-07", "07:00", "Jane Smith", 2);

        // Act
        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=2026-02-07");

        // Assert
        var teeSheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        var bookedSlot = teeSheet!.Slots.First(s => s.Status == "booked");

        Assert.Equal("Jane Smith", bookedSlot.GolferName);
        Assert.Equal(2, bookedSlot.PlayerCount);
    }

    [Fact]
    public async Task GetTeeSheet_CourseNotFound_ReturnsNotFound()
    {
        var response = await this.client.GetAsync($"/tee-sheets?courseId={Guid.NewGuid()}&date=2026-02-07");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTeeSheet_TeeTimeSettingsNotConfigured_ReturnsNotFound()
    {
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var response = await this.client.GetAsync($"/tee-sheets?courseId={course!.Id}&date=2026-02-07");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTeeSheet_InvalidDateFormat_ReturnsBadRequest()
    {
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        var response = await this.client.GetAsync($"/tee-sheets?courseId={course!.Id}&date=02-07-2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTeeSheet_MissingCourseId_ReturnsBadRequest()
    {
        var response = await this.client.GetAsync("/tee-sheets?date=2026-02-07");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTeeSheet_MissingDate_ReturnsBadRequest()
    {
        var response = await this.client.GetAsync($"/tee-sheets?courseId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTeeSheet_EmptyTeeSheet_ReturnsAllOpenSlots()
    {
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "08:00"
        });

        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=2026-02-07");

        var teeSheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        Assert.All(teeSheet!.Slots, slot =>
        {
            Assert.Equal("open", slot.Status);
            Assert.Null(slot.GolferName);
            Assert.Equal(0, slot.PlayerCount);
        });
    }

    [Fact]
    public async Task GetTeeSheet_FullyBookedTeeSheet_ReturnsAllBookedSlots()
    {
        var tenantId = await CreateTestTenantAsync();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        createRequest.Content = JsonContent.Create(new { Name = "Test Course", TimeZoneId = TestTimeZones.Chicago });
        var createResponse = await this.client.SendAsync(createRequest);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseResponse>();

        await this.client.PutAsJsonAsync($"/courses/{course!.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "07:30"
        });

        // Book all slots
        await CreateBookingAsync(course.Id, "2026-02-07", "07:00", "Player 1", 4);
        await CreateBookingAsync(course.Id, "2026-02-07", "07:10", "Player 2", 4);
        await CreateBookingAsync(course.Id, "2026-02-07", "07:20", "Player 3", 4);

        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=2026-02-07");

        var teeSheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        Assert.Equal(3, teeSheet!.Slots.Count);
        Assert.All(teeSheet.Slots, slot => Assert.Equal("booked", slot.Status));
    }

    private record CourseResponse(
        Guid Id,
        string Name,
        string? StreetAddress,
        string? City,
        string? State,
        string? ZipCode,
        string? ContactEmail,
        string? ContactPhone,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private record TenantResponse(Guid Id);

    private record TeeSheetResponse(
        Guid CourseId,
        string CourseName,
        List<TeeSheetSlot> Slots);

    private record TeeSheetSlot(
        DateTime TeeTime,
        string Status,
        string? GolferName,
        int PlayerCount);
}
