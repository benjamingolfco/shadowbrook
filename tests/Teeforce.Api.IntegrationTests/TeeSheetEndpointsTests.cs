using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.TeeSheetAggregate;

namespace Teeforce.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class TeeSheetEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly TestWebApplicationFactory factory = factory;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await this.factory.ResetDatabaseAsync();
        await this.factory.SeedTestAdminAsync();
        this.client = this.factory.CreateAuthenticatedClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CreateGolferAsync(string firstName, string lastName)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var phone = $"+1555{new Random().Next(1000000, 9999999)}";
        var golfer = Golfer.Create(phone, firstName, lastName);
        db.Golfers.Add(golfer);
        await db.SaveChangesAsync();
        return golfer.Id;
    }

    private async Task DraftAndPublishSheetAsync(Guid courseId, string date)
    {
        var draftResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-sheets/draft",
            new { Date = date });
        draftResponse.EnsureSuccessStatusCode();

        var publishResponse = await this.client.PostAsync(
            $"/courses/{courseId}/tee-sheets/{date}/publish",
            content: null);
        publishResponse.EnsureSuccessStatusCode();
    }

    private async Task<List<TeeSheetSlot>> GetSlotsAsync(Guid courseId, string date)
    {
        var response = await this.client.GetAsync($"/tee-sheets?courseId={courseId}&date={date}");
        response.EnsureSuccessStatusCode();
        var sheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        return sheet!.Slots;
    }

    private async Task BookSlotAsync(Guid courseId, string date, string time, string golferName, int playerCount)
    {
        var nameParts = golferName.Split(' ', 2);
        var firstName = nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;
        var golferId = await CreateGolferAsync(firstName, lastName);

        // Look up the interval id by matching the slot's TeeTime wall-clock value.
        var targetDateTime = DateOnly.ParseExact(date, "yyyy-MM-dd")
            .ToDateTime(TimeOnly.ParseExact(time, "HH:mm"));

        using var scope = this.factory.Services.CreateScope();
        var teeSheetRepository = scope.ServiceProvider.GetRequiredService<ITeeSheetRepository>();
        var parsedDate = DateOnly.ParseExact(date, "yyyy-MM-dd");
        var parsedTime = TimeOnly.ParseExact(time, "HH:mm");
        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId, parsedDate);
        var interval = sheet!.Intervals.Single(i => i.Time == parsedTime);

        var bookResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-times/book",
            new
            {
                BookingId = Guid.CreateVersion7(),
                TeeSheetIntervalId = interval.Id,
                GolferId = golferId,
                GroupSize = playerCount
            });
        bookResponse.EnsureSuccessStatusCode();
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

    private async Task<CourseIdResponse> CreateCourseAsync(Guid tenantId, string name = "Test Course")
    {
        var response = await this.client.PostAsJsonAsync("/courses", new { Name = name, OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        return (await response.Content.ReadFromJsonAsync<CourseIdResponse>())!;
    }

    [Fact]
    public async Task GetTeeSheet_WithValidCourseAndDate_ReturnsOk()
    {
        // Arrange - Create course with tee time settings
        var tenantId = await CreateTestTenantAsync();
        var course = await CreateCourseAsync(tenantId);

        await this.client.PutAsJsonAsync($"/courses/{course.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "17:00",
            DefaultCapacity = 4
        });

        await DraftAndPublishSheetAsync(course.Id, "2026-02-07");

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
        var course = await CreateCourseAsync(tenantId);

        await this.client.PutAsJsonAsync($"/courses/{course.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "08:00",
            DefaultCapacity = 4
        });

        await DraftAndPublishSheetAsync(course.Id, "2026-02-07");

        // Create a booking at 07:10
        await BookSlotAsync(course.Id, "2026-02-07", "07:10", "John Doe", 4);

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
        var course = await CreateCourseAsync(tenantId);

        await this.client.PutAsJsonAsync($"/courses/{course.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "08:00",
            DefaultCapacity = 4
        });

        await DraftAndPublishSheetAsync(course.Id, "2026-02-07");
        await BookSlotAsync(course.Id, "2026-02-07", "07:00", "Jane Smith", 2);

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
    public async Task GetTeeSheet_InvalidDateFormat_ReturnsBadRequest()
    {
        var tenantId = await CreateTestTenantAsync();
        var course = await CreateCourseAsync(tenantId);

        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=02-07-2026");

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
        var course = await CreateCourseAsync(tenantId);

        await this.client.PutAsJsonAsync($"/courses/{course.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "08:00",
            DefaultCapacity = 4
        });

        await DraftAndPublishSheetAsync(course.Id, "2026-02-07");

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
        var course = await CreateCourseAsync(tenantId);

        await this.client.PutAsJsonAsync($"/courses/{course.Id}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "07:30",
            DefaultCapacity = 4
        });

        await DraftAndPublishSheetAsync(course.Id, "2026-02-07");

        // Book all slots
        await BookSlotAsync(course.Id, "2026-02-07", "07:00", "Player 1", 4);
        await BookSlotAsync(course.Id, "2026-02-07", "07:10", "Player 2", 4);
        await BookSlotAsync(course.Id, "2026-02-07", "07:20", "Player 3", 4);

        var response = await this.client.GetAsync($"/tee-sheets?courseId={course.Id}&date=2026-02-07");

        var teeSheet = await response.Content.ReadFromJsonAsync<TeeSheetResponse>();
        Assert.Equal(3, teeSheet!.Slots.Count);
        Assert.All(teeSheet.Slots, slot => Assert.Equal("booked", slot.Status));
    }
}
