using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestServiceTests
{
    private readonly ITeeTimeRequestRepository teeTimeRequestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IWalkUpWaitlistRepository waitlistRepo = Substitute.For<IWalkUpWaitlistRepository>();
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();

    // A fixed "now" in America/Chicago (UTC-6): 2026-03-06 10:00 local = 2026-03-06 16:00 UTC
    private const string ChicagoTz = "America/Chicago";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 3, 6, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 6);
    private static readonly TimeOnly CurrentTime = new(10, 0);

    public TeeTimeRequestServiceTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

    private TeeTimeRequestService CreateService() =>
        new(this.teeTimeRequestRepo, this.waitlistRepo);

    private async Task<WalkUpWaitlist> OpenWaitlistForDate(Guid courseId, DateOnly date)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(courseId, date, this.shortCodeGenerator, this.waitlistRepo);
        this.waitlistRepo.GetOpenByCourseDateAsync(courseId, date).Returns(waitlist);
        return waitlist;
    }

    // -------------------------------------------------------------------------
    // Existing tests — updated to pass TimeProvider
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenWaitlistIsOpen_CreatesRequest()
    {
        var courseId = Guid.NewGuid();
        await OpenWaitlistForDate(courseId, Today);

        var fakeTime = new FakeTimeProvider(FixedUtcNow);
        var teeTime = new TimeOnly(10, 30); // 30 min in the future

        var request = await CreateService().CreateAsync(courseId, Today, teeTime, 2, ChicagoTz, fakeTime);

        Assert.Equal(courseId, request.CourseId);
        Assert.Equal(Today, request.Date);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
    }

    [Fact]
    public async Task CreateAsync_WhenNoOpenWaitlist_Throws()
    {
        var courseId = Guid.NewGuid();
        var fakeTime = new FakeTimeProvider(FixedUtcNow);

        // NSubstitute returns null by default — no setup needed

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => CreateService().CreateAsync(courseId, Today, new TimeOnly(10, 30), 2, ChicagoTz, fakeTime));
    }

    [Fact]
    public async Task CreateAsync_WhenWaitlistClosed_Throws()
    {
        var courseId = Guid.NewGuid();
        var fakeTime = new FakeTimeProvider(FixedUtcNow);

        var waitlist = await WalkUpWaitlist.OpenAsync(courseId, Today, this.shortCodeGenerator, this.waitlistRepo);
        waitlist.Close();
        // Closed waitlist should NOT be returned by GetOpenByCourseDateAsync
        // so we don't configure the repo to return it

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => CreateService().CreateAsync(courseId, Today, new TimeOnly(10, 30), 2, ChicagoTz, fakeTime));
    }

    // -------------------------------------------------------------------------
    // Past tee time validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PastDate_ThrowsTeeTimePastException()
    {
        var courseId = Guid.NewGuid();
        var fakeTime = new FakeTimeProvider(FixedUtcNow);
        var yesterday = Today.AddDays(-1);
        await OpenWaitlistForDate(courseId, yesterday);

        await Assert.ThrowsAsync<TeeTimePastException>(
            () => CreateService().CreateAsync(courseId, yesterday, new TimeOnly(10, 30), 2, ChicagoTz, fakeTime));
    }

    [Fact]
    public async Task CreateAsync_TodayTeeTimeBeforeGracePeriod_ThrowsTeeTimePastException()
    {
        var courseId = Guid.NewGuid();
        var fakeTime = new FakeTimeProvider(FixedUtcNow);
        // Current local time is 10:00 — tee time at 09:50 is 10 min ago, outside the 5-min grace period
        var pastTeeTime = CurrentTime.Add(TimeSpan.FromMinutes(-10));
        await OpenWaitlistForDate(courseId, Today);

        await Assert.ThrowsAsync<TeeTimePastException>(
            () => CreateService().CreateAsync(courseId, Today, pastTeeTime, 2, ChicagoTz, fakeTime));
    }

    [Fact]
    public async Task CreateAsync_TodayTeeTimeWithinGracePeriod_DoesNotThrow()
    {
        var courseId = Guid.NewGuid();
        var fakeTime = new FakeTimeProvider(FixedUtcNow);
        // Current local time is 10:00 — tee time at 09:56 is 4 min ago, within the 5-min grace period
        var recentTeeTime = CurrentTime.Add(TimeSpan.FromMinutes(-4));
        await OpenWaitlistForDate(courseId, Today);

        var request = await CreateService().CreateAsync(courseId, Today, recentTeeTime, 2, ChicagoTz, fakeTime);

        Assert.Equal(recentTeeTime, request.TeeTime);
    }

    [Fact]
    public async Task CreateAsync_FutureDate_DoesNotThrow()
    {
        var courseId = Guid.NewGuid();
        var fakeTime = new FakeTimeProvider(FixedUtcNow);
        var tomorrow = Today.AddDays(1);
        await OpenWaitlistForDate(courseId, tomorrow);

        var request = await CreateService().CreateAsync(courseId, tomorrow, new TimeOnly(8, 0), 2, ChicagoTz, fakeTime);

        Assert.Equal(tomorrow, request.Date);
    }
}
