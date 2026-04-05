using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Handlers;

public class WakeUpOfferPolicyHandlerTests
{
    private readonly ICourseWaitlistRepository waitlistRepo = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly IShortCodeGenerator shortCodeGen = Substitute.For<IShortCodeGenerator>();
    private readonly WaitlistMatchingService matchingService;

    private static readonly DateOnly Date = new(2026, 6, 1);
    private static readonly Guid CourseId = Guid.NewGuid();

    public WakeUpOfferPolicyHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(new DateOnly(2026, 3, 25));
        this.shortCodeGen.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC");
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.matchingService = new WaitlistMatchingService(this.entryRepo, this.openingRepo);
    }

    private async Task<(WalkUpWaitlist waitlist, GolferWaitlistEntry entry)> CreateWaitlistAndEntryAsync(
        TimeOnly joinTime)
    {
        var joinProvider = Substitute.For<ITimeProvider>();
        joinProvider.GetCurrentTimestamp()
            .Returns(new DateTimeOffset(Date.ToDateTime(joinTime), TimeSpan.Zero));
        joinProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(Date);
        joinProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(joinTime);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            CourseId, Date, this.shortCodeGen, this.waitlistRepo, joinProvider);

        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);

        var golfer = Golfer.Create("+15551234567", "Test", "Golfer");
        var entry = await waitlist.Join(golfer, this.entryRepo, joinProvider, "UTC");

        return (waitlist, entry);
    }

    [Fact]
    public async Task Handle_WaitlistNotFound_Throws()
    {
        var evt = new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = Guid.NewGuid(),
            CourseWaitlistId = Guid.NewGuid(),
            GolferId = Guid.NewGuid()
        };
        this.waitlistRepo.GetByIdAsync(evt.CourseWaitlistId).Returns((CourseWaitlist?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => GolferJoinedWaitlistWakeUpHandler.Handle(
                evt, this.waitlistRepo, this.entryRepo, this.matchingService,
                NullLogger.Instance, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EntryNotFound_LogsAndReturnsNull()
    {
        var (waitlist, _) = await CreateWaitlistAndEntryAsync(new TimeOnly(9, 0));
        var evt = new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = Guid.NewGuid(),
            CourseWaitlistId = waitlist.Id,
            GolferId = Guid.NewGuid()
        };
        this.waitlistRepo.GetByIdAsync(waitlist.Id).Returns(waitlist);
        this.entryRepo.GetByIdAsync(evt.GolferWaitlistEntryId).Returns((GolferWaitlistEntry?)null);

        var result = await GolferJoinedWaitlistWakeUpHandler.Handle(
            evt, this.waitlistRepo, this.entryRepo, this.matchingService,
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_NoActiveOpeningForGolfer_ReturnsNull()
    {
        var (waitlist, entry) = await CreateWaitlistAndEntryAsync(new TimeOnly(9, 0));
        var evt = new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = waitlist.Id,
            GolferId = entry.GolferId
        };
        this.waitlistRepo.GetByIdAsync(waitlist.Id).Returns(waitlist);
        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);
        this.openingRepo
            .FindActiveOpeningsForCourseDateAsync(CourseId, Date, Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening>());

        var result = await GolferJoinedWaitlistWakeUpHandler.Handle(
            evt, this.waitlistRepo, this.entryRepo, this.matchingService,
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_OpeningFoundInWindow_ReturnsWakeUpOfferPolicy()
    {
        // Entry window: 09:00 - 09:30; opening at 09:15 is inside the window
        var (waitlist, entry) = await CreateWaitlistAndEntryAsync(new TimeOnly(9, 0));
        var evt = new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = waitlist.Id,
            GolferId = entry.GolferId
        };
        this.waitlistRepo.GetByIdAsync(waitlist.Id).Returns(waitlist);
        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var opening = TeeTimeOpening.Create(CourseId, Date, new TimeOnly(9, 15), 2, false, this.timeProvider);
        this.openingRepo
            .FindActiveOpeningsForCourseDateAsync(CourseId, Date, Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening> { opening });

        var result = await GolferJoinedWaitlistWakeUpHandler.Handle(
            evt, this.waitlistRepo, this.entryRepo, this.matchingService,
            NullLogger.Instance, CancellationToken.None);

        var wakeUp = Assert.IsType<WakeUpOfferPolicy>(result);
        Assert.Equal(opening.Id, wakeUp.OpeningId);
    }

    [Fact]
    public async Task Handle_OpeningOutsideWindow_ReturnsNull()
    {
        // Entry window: 09:00 - 09:30; opening at 11:00 is outside the window
        var (waitlist, entry) = await CreateWaitlistAndEntryAsync(new TimeOnly(9, 0));
        var evt = new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = waitlist.Id,
            GolferId = entry.GolferId
        };
        this.waitlistRepo.GetByIdAsync(waitlist.Id).Returns(waitlist);
        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var opening = TeeTimeOpening.Create(CourseId, Date, new TimeOnly(11, 0), 2, false, this.timeProvider);
        this.openingRepo
            .FindActiveOpeningsForCourseDateAsync(CourseId, Date, Arg.Any<CancellationToken>())
            .Returns(new List<TeeTimeOpening> { opening });

        var result = await GolferJoinedWaitlistWakeUpHandler.Handle(
            evt, this.waitlistRepo, this.entryRepo, this.matchingService,
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
    }
}
