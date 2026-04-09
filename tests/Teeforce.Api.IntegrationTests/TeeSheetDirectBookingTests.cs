using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
public class TeeSheetDirectBookingTests(TestWebApplicationFactory factory) : IAsyncLifetime
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
        var phone = $"+1555{Random.Shared.Next(1000000, 9999999)}";
        var golfer = Golfer.Create(phone, firstName, lastName);
        db.Golfers.Add(golfer);
        await db.SaveChangesAsync();
        return golfer.Id;
    }

    private async Task<Guid> CreateCourseWithSettingsAsync(string firstTime = "07:00", string lastTime = "08:00")
    {
        var tenantId = await TestSetup.CreateTenantAsync(this.client);
        var courseResponse = await this.client.PostAsJsonAsync(
            "/courses",
            new { Name = "Test Course", OrganizationId = tenantId, TimeZoneId = TestTimeZones.Chicago });
        courseResponse.EnsureSuccessStatusCode();
        var course = (await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>())!;

        var settingsResponse = await this.client.PutAsJsonAsync(
            $"/courses/{course.Id}/tee-time-settings",
            new
            {
                TeeTimeIntervalMinutes = 10,
                FirstTeeTime = firstTime,
                LastTeeTime = lastTime
            });
        settingsResponse.EnsureSuccessStatusCode();

        return course.Id;
    }

    private async Task DraftSheetAsync(Guid courseId, string date)
    {
        var draftResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-sheets/draft",
            new { Date = date });
        draftResponse.EnsureSuccessStatusCode();
    }

    private async Task PublishSheetAsync(Guid courseId, string date)
    {
        var publishResponse = await this.client.PostAsync(
            $"/courses/{courseId}/tee-sheets/{date}/publish",
            content: null);
        publishResponse.EnsureSuccessStatusCode();
    }

    private async Task<Guid> GetFirstIntervalIdAsync(Guid courseId, string date)
    {
        using var scope = this.factory.Services.CreateScope();
        var teeSheetRepository = scope.ServiceProvider.GetRequiredService<ITeeSheetRepository>();
        var parsedDate = DateOnly.ParseExact(date, "yyyy-MM-dd");
        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId, parsedDate);
        return sheet!.Intervals.OrderBy(i => i.Time).First().Id;
    }

    [Fact]
    public async Task DraftPublishBook_HappyPath()
    {
        // Arrange
        var courseId = await CreateCourseWithSettingsAsync();
        const string date = "2026-07-15";

        await DraftSheetAsync(courseId, date);
        await PublishSheetAsync(courseId, date);

        var intervalId = await GetFirstIntervalIdAsync(courseId, date);
        var golferId = await CreateGolferAsync("Happy", "Path");
        var bookingId = Guid.CreateVersion7();

        // Act
        var bookResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-times/book",
            new
            {
                BookingId = bookingId,
                TeeSheetIntervalId = intervalId,
                GolferId = golferId,
                GroupSize = 2
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, bookResponse.StatusCode);

        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = await db.Bookings.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == bookingId);
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Confirmed, booking!.Status);
        Assert.NotNull(booking.TeeTimeId);

        var teeTime = await db.TeeTimes
            .Include(t => t.Claims)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeeSheetIntervalId == intervalId);
        Assert.NotNull(teeTime);
        Assert.Equal(TeeTimeStatus.Open, teeTime!.Status);
        Assert.Equal(2, teeTime.Remaining);
        Assert.Equal(4, teeTime.Capacity);
        Assert.Equal(booking.TeeTimeId, teeTime.Id);

        var claim = Assert.Single(teeTime.Claims);
        Assert.Equal(bookingId, claim.BookingId);
        Assert.Equal(2, claim.GroupSize);
    }

    [Fact]
    public async Task BookingAgainstDraftSheet_Returns409()
    {
        // Arrange
        var courseId = await CreateCourseWithSettingsAsync();
        const string date = "2026-07-16";

        await DraftSheetAsync(courseId, date);
        // Intentionally do NOT publish.

        var intervalId = await GetFirstIntervalIdAsync(courseId, date);
        var golferId = await CreateGolferAsync("Draft", "Booker");

        // Act
        var bookResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-times/book",
            new
            {
                BookingId = Guid.CreateVersion7(),
                TeeSheetIntervalId = intervalId,
                GolferId = golferId,
                GroupSize = 2
            });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, bookResponse.StatusCode);
    }

    [Fact]
    public async Task FillScenario_NextBookingExceedsCapacity_Returns409()
    {
        // Arrange — default capacity is 4 (a foursome).
        var courseId = await CreateCourseWithSettingsAsync();
        const string date = "2026-07-17";

        await DraftSheetAsync(courseId, date);
        await PublishSheetAsync(courseId, date);

        var intervalId = await GetFirstIntervalIdAsync(courseId, date);

        // Fill the slot with four single-player bookings.
        for (var i = 0; i < 4; i++)
        {
            var golferId = await CreateGolferAsync($"Player{i}", "Solo");
            var fillResponse = await this.client.PostAsJsonAsync(
                $"/courses/{courseId}/tee-times/book",
                new
                {
                    BookingId = Guid.CreateVersion7(),
                    TeeSheetIntervalId = intervalId,
                    GolferId = golferId,
                    GroupSize = 1
                });
            Assert.Equal(HttpStatusCode.OK, fillResponse.StatusCode);
        }

        // Act — fifth booking on a now-filled tee time.
        var fifthGolferId = await CreateGolferAsync("Fifth", "Wheel");
        var overflowResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-times/book",
            new
            {
                BookingId = Guid.CreateVersion7(),
                TeeSheetIntervalId = intervalId,
                GolferId = fifthGolferId,
                GroupSize = 1
            });

        // Assert — TeeTimeFilledException is thrown first (Status == Filled / Remaining == 0),
        // which DomainExceptionHandler maps to 409 Conflict.
        Assert.Equal(HttpStatusCode.Conflict, overflowResponse.StatusCode);
    }

    [Fact]
    public async Task TeeSheetView_ReturnsBookedSlot()
    {
        // Arrange
        var courseId = await CreateCourseWithSettingsAsync();
        const string date = "2026-07-18";

        await DraftSheetAsync(courseId, date);
        await PublishSheetAsync(courseId, date);

        var intervalId = await GetFirstIntervalIdAsync(courseId, date);
        var golferId = await CreateGolferAsync("Alice", "Anderson");

        var bookResponse = await this.client.PostAsJsonAsync(
            $"/courses/{courseId}/tee-times/book",
            new
            {
                BookingId = Guid.CreateVersion7(),
                TeeSheetIntervalId = intervalId,
                GolferId = golferId,
                GroupSize = 3
            });
        bookResponse.EnsureSuccessStatusCode();

        // Act
        var sheetResponse = await this.client.GetAsync($"/tee-sheets?courseId={courseId}&date={date}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, sheetResponse.StatusCode);
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<TeeSheetResponse>();
        Assert.NotNull(sheet);

        // The booked interval is the first one (07:00).
        var bookedSlot = sheet!.Slots.First();
        Assert.Equal("booked", bookedSlot.Status);
        Assert.Equal("Alice Anderson", bookedSlot.GolferName);
        Assert.Equal(3, bookedSlot.PlayerCount);
    }
}
