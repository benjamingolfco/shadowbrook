using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Domain.Tests.WaitlistServices;

public class WaitlistMatchingServiceTests
{
    private readonly IGolferWaitlistEntryRepository entryRepository =
        Substitute.For<IGolferWaitlistEntryRepository>();

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
        this.sut = new WaitlistMatchingService(this.entryRepository);
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

    [Fact]
    public async Task FindEligibleEntries_WhenOpeningNotOpen_ThrowsOpeningNotAvailable()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 6, 15), new TimeOnly(8, 0),
            slotsAvailable: 2, operatorOwned: true, timeProvider: this.timeProvider);
        opening.Expire(this.timeProvider);

        await Assert.ThrowsAsync<OpeningNotAvailableException>(
            () => this.sut.FindEligibleEntriesAsync(opening));
    }
}
