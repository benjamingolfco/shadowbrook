using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestServiceTests
{
    private readonly ITeeTimeRequestRepository teeTimeRequestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IWalkUpWaitlistRepository waitlistRepo = Substitute.For<IWalkUpWaitlistRepository>();
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseTimeZoneProvider courseTimeZoneProvider = Substitute.For<ICourseTimeZoneProvider>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    private const string ChicagoTz = "America/Chicago";
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 3, 6);
    private static readonly TimeOnly CurrentTime = new(10, 0);

    public TeeTimeRequestServiceTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
        this.courseTimeZoneProvider.GetTimeZoneIdAsync(CourseId).Returns(ChicagoTz);
        this.timeProvider.GetCurrentDateByTimeZone(ChicagoTz).Returns(Today);
        this.timeProvider.GetCurrentTimeByTimeZone(ChicagoTz).Returns(CurrentTime);
    }

    private TeeTimeRequestService CreateService() =>
        new(this.teeTimeRequestRepo, this.waitlistRepo, this.courseTimeZoneProvider, this.timeProvider);

    private async Task<WalkUpWaitlist> OpenWaitlistForDate(Guid courseId, DateOnly date)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(courseId, date, this.shortCodeGenerator, this.waitlistRepo);
        this.waitlistRepo.GetOpenByCourseDateAsync(courseId, date).Returns(waitlist);
        return waitlist;
    }

    // -------------------------------------------------------------------------
    // Existing tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenWaitlistIsOpen_CreatesRequest()
    {
        await OpenWaitlistForDate(CourseId, Today);

        var teeTime = new TimeOnly(10, 30); // 30 min in the future

        var request = await CreateService().CreateAsync(CourseId, Today, teeTime, 2);

        Assert.Equal(CourseId, request.CourseId);
        Assert.Equal(Today, request.Date);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
    }

    [Fact]
    public async Task CreateAsync_WhenNoOpenWaitlist_Throws()
    {
        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => CreateService().CreateAsync(CourseId, Today, new TimeOnly(10, 30), 2));
    }

    [Fact]
    public async Task CreateAsync_WhenWaitlistClosed_Throws()
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(CourseId, Today, this.shortCodeGenerator, this.waitlistRepo);
        waitlist.Close();

        await Assert.ThrowsAsync<WaitlistNotOpenForRequestsException>(
            () => CreateService().CreateAsync(CourseId, Today, new TimeOnly(10, 30), 2));
    }

    // -------------------------------------------------------------------------
    // Past tee time validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PastDate_ThrowsTeeTimePastException()
    {
        var yesterday = Today.AddDays(-1);
        await OpenWaitlistForDate(CourseId, yesterday);

        await Assert.ThrowsAsync<TeeTimePastException>(
            () => CreateService().CreateAsync(CourseId, yesterday, new TimeOnly(10, 30), 2));
    }

    [Fact]
    public async Task CreateAsync_TodayTeeTimeBeforeGracePeriod_ThrowsTeeTimePastException()
    {
        var pastTeeTime = CurrentTime.Add(TimeSpan.FromMinutes(-10));
        await OpenWaitlistForDate(CourseId, Today);

        await Assert.ThrowsAsync<TeeTimePastException>(
            () => CreateService().CreateAsync(CourseId, Today, pastTeeTime, 2));
    }

    [Fact]
    public async Task CreateAsync_TodayTeeTimeWithinGracePeriod_DoesNotThrow()
    {
        var recentTeeTime = CurrentTime.Add(TimeSpan.FromMinutes(-4));
        await OpenWaitlistForDate(CourseId, Today);

        var request = await CreateService().CreateAsync(CourseId, Today, recentTeeTime, 2);

        Assert.Equal(recentTeeTime, request.TeeTime);
    }

    [Fact]
    public async Task CreateAsync_FutureDate_DoesNotThrow()
    {
        var tomorrow = Today.AddDays(1);
        await OpenWaitlistForDate(CourseId, tomorrow);

        var request = await CreateService().CreateAsync(CourseId, tomorrow, new TimeOnly(8, 0), 2);

        Assert.Equal(tomorrow, request.Date);
    }
}
