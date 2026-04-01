using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Domain.Tests.WaitlistServices;

public class WaitlistMatchingServiceTests
{
    private readonly IGolferWaitlistEntryRepository entryRepository =
        Substitute.For<IGolferWaitlistEntryRepository>();

    private readonly ITeeTimeOpeningRepository openingRepository =
        Substitute.For<ITeeTimeOpeningRepository>();

    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepository = Substitute.For<ICourseWaitlistRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly WaitlistMatchingService sut;

    public WaitlistMatchingServiceTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 3, 25));
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.sut = new WaitlistMatchingService(this.entryRepository, this.openingRepository);
    }

    private async Task<GolferWaitlistEntry> CreateRealEntryAsync()
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            this.shortCodeGenerator, this.waitlistRepository, this.timeProvider);
        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");
        return await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC");
    }

    [Fact]
    public async Task FindEligibleEntries_PassesOpeningParametersToRepository()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 15);
        var teeTime = new TimeOnly(8, 0);
        var opening = TeeTimeOpening.Create(courseId, date, teeTime, slotsAvailable: 3, operatorOwned: false, timeProvider: this.timeProvider);

        this.entryRepository
            .FindEligibleEntriesAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await this.sut.FindEligibleEntriesAsync(opening);

        await this.entryRepository.Received(1).FindEligibleEntriesAsync(
            Arg.Is(courseId),
            Arg.Is(date),
            Arg.Is(teeTime),
            Arg.Is(3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindEligibleEntries_ReturnsRepositoryResults()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(8, 0),
            slotsAvailable: 2,
            operatorOwned: false,
            timeProvider: this.timeProvider);

        var entry = await CreateRealEntryAsync();
        var expected = new List<GolferWaitlistEntry> { entry };

        this.entryRepository
            .FindEligibleEntriesAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await this.sut.FindEligibleEntriesAsync(opening);

        Assert.Same(expected, result);
    }

    // FindOpeningForGolferAsync tests
    // Entry window is set from join time (timeProvider at 10:00) + 30 min = [10:00, 10:30].

    private async Task<GolferWaitlistEntry> CreateEntryWithWindowAsync(TimeOnly joinTime, DateOnly date)
    {
        var joinProvider = Substitute.For<ITimeProvider>();
        joinProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(date.ToDateTime(joinTime), TimeSpan.Zero));
        joinProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(date);
        joinProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(joinTime);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), date, this.shortCodeGenerator, this.waitlistRepository, joinProvider);

        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");
        return await waitlist.Join(golfer, this.entryRepository, joinProvider, "UTC");
    }

    [Fact]
    public async Task FindOpeningForGolfer_NoActiveOpenings_ReturnsNull()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);
        var entry = await CreateEntryWithWindowAsync(new TimeOnly(9, 0), date);

        this.openingRepository
            .FindActiveOpeningsForCourseDateAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening>());

        var result = await this.sut.FindOpeningForGolferAsync(entry, courseId, date);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindOpeningForGolfer_OpeningOutsideWindow_ReturnsNull()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);

        // Entry window: 09:00 - 09:30
        var entry = await CreateEntryWithWindowAsync(new TimeOnly(9, 0), date);

        // Opening at 10:00 — outside golfer's window
        var opening = TeeTimeOpening.Create(courseId, date, new TimeOnly(10, 0), 2, false, this.timeProvider);
        this.openingRepository
            .FindActiveOpeningsForCourseDateAsync(courseId, date, Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening> { opening });

        var result = await this.sut.FindOpeningForGolferAsync(entry, courseId, date);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindOpeningForGolfer_OpeningInsideWindow_ReturnsOpening()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);

        // Entry window: 09:00 - 09:30
        var entry = await CreateEntryWithWindowAsync(new TimeOnly(9, 0), date);

        // Opening at 09:15 — inside golfer's window
        var opening = TeeTimeOpening.Create(courseId, date, new TimeOnly(9, 15), 2, false, this.timeProvider);
        this.openingRepository
            .FindActiveOpeningsForCourseDateAsync(courseId, date, Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening> { opening });

        var result = await this.sut.FindOpeningForGolferAsync(entry, courseId, date);

        Assert.NotNull(result);
        Assert.Equal(opening.Id, result.Id);
    }

    [Fact]
    public async Task FindOpeningForGolfer_MultipleOpenings_ReturnsFirstMatchingWindow()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);

        // Entry window: 09:00 - 09:30
        var entry = await CreateEntryWithWindowAsync(new TimeOnly(9, 0), date);

        // Opening at 08:00 — before window; opening at 09:10 — inside window
        var outsideOpening = TeeTimeOpening.Create(courseId, date, new TimeOnly(8, 0), 2, false, this.timeProvider);
        var insideOpening = TeeTimeOpening.Create(courseId, date, new TimeOnly(9, 10), 2, false, this.timeProvider);

        this.openingRepository
            .FindActiveOpeningsForCourseDateAsync(courseId, date, Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening> { outsideOpening, insideOpening });

        var result = await this.sut.FindOpeningForGolferAsync(entry, courseId, date);

        Assert.NotNull(result);
        Assert.Equal(insideOpening.Id, result.Id);
    }

}
