using NSubstitute;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Domain.Tests.WaitlistServices;

public class WaitlistMatchingServiceTests
{
    private readonly IGolferWaitlistEntryRepository entryRepository =
        Substitute.For<IGolferWaitlistEntryRepository>();

    private readonly WaitlistMatchingService sut;

    public WaitlistMatchingServiceTests()
    {
        this.sut = new WaitlistMatchingService(this.entryRepository);
    }

    [Fact]
    public async Task FindEligibleEntries_PassesOpeningParametersToRepository()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 15);
        var teeTime = new TimeOnly(8, 0);
        var opening = TeeTimeOpening.Create(courseId, date, teeTime, slotsAvailable: 3, operatorOwned: false);

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
            operatorOwned: false);

        var entry = Substitute.For<GolferWaitlistEntry>();
        var expected = new List<GolferWaitlistEntry> { entry };

        this.entryRepository
            .FindEligibleEntriesAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await this.sut.FindEligibleEntriesAsync(opening);

        Assert.Same(expected, result);
    }
}
