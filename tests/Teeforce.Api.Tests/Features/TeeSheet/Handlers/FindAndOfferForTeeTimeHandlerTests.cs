using System.Reflection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.TeeSheet.Handlers;
using Teeforce.Api.Features.TeeSheet.Policies;
using Teeforce.Domain.Common;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.TeeTimeOfferAggregate;
using Teeforce.Domain.WaitlistServices;

namespace Teeforce.Api.Tests.Features.TeeSheet.Handlers;

public class FindAndOfferForTeeTimeHandlerTests
{
    private readonly ITeeTimeWaitlistMatcher matcher = Substitute.For<ITeeTimeWaitlistMatcher>();
    private readonly ITeeTimeOfferRepository offerRepository = Substitute.For<ITeeTimeOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    private readonly Guid teeTimeId = Guid.NewGuid();
    private readonly Guid courseId = Guid.NewGuid();
    private readonly DateOnly date = new(2026, 6, 1);
    private readonly TimeOnly time = new(9, 0);

    public FindAndOfferForTeeTimeHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private FindAndOfferForTeeTime MakeCommand(int availableSlots = 2) =>
        new(this.teeTimeId, availableSlots, this.courseId, this.date, this.time);

    [Fact]
    public async Task Handle_WhenNoEligibleEntries_DoesNotCreateOffers()
    {
        this.matcher.FindEligibleEntries(
            this.teeTimeId, this.courseId, this.date, this.time, 2, Arg.Any<CancellationToken>())
            .Returns([]);

        await FindAndOfferForTeeTimeHandler.Handle(
            MakeCommand(), this.matcher, this.offerRepository,
            this.timeProvider, this.logger, CancellationToken.None);

        this.offerRepository.DidNotReceive().Add(Arg.Any<TeeTimeOffer>());
    }

    [Fact]
    public async Task Handle_WithEligibleEntries_CreatesOffers()
    {
        var entry = CreateEntry();
        this.matcher.FindEligibleEntries(
            this.teeTimeId, this.courseId, this.date, this.time, 2, Arg.Any<CancellationToken>())
            .Returns([entry]);

        await FindAndOfferForTeeTimeHandler.Handle(
            MakeCommand(), this.matcher, this.offerRepository,
            this.timeProvider, this.logger, CancellationToken.None);

        this.offerRepository.Received(1).Add(Arg.Any<TeeTimeOffer>());
    }

    [Fact]
    public async Task Handle_LimitsOffersToAvailableSlots()
    {
        var entries = new List<GolferWaitlistEntry>
        {
            CreateEntry(),
            CreateEntry(),
            CreateEntry(),
        };
        this.matcher.FindEligibleEntries(
            this.teeTimeId, this.courseId, this.date, this.time, 1, Arg.Any<CancellationToken>())
            .Returns(entries);

        await FindAndOfferForTeeTimeHandler.Handle(
            MakeCommand(availableSlots: 1), this.matcher, this.offerRepository,
            this.timeProvider, this.logger, CancellationToken.None);

        this.offerRepository.Received(1).Add(Arg.Any<TeeTimeOffer>());
    }

    private static GolferWaitlistEntry CreateEntry()
    {
        // GolferWaitlistEntry is abstract with internal constructors — use reflection
        // to invoke the private EF parameterless constructor on OnlineGolferWaitlistEntry,
        // then set the properties the handler reads (Id, GolferId, GroupSize).
        var ctor = typeof(OnlineGolferWaitlistEntry)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var entry = (GolferWaitlistEntry)ctor.Invoke(null);

        typeof(Entity).GetProperty(nameof(Entity.Id))!
            .SetValue(entry, Guid.NewGuid());
        typeof(GolferWaitlistEntry).GetProperty(nameof(GolferWaitlistEntry.GolferId))!
            .GetSetMethod(nonPublic: true)!.Invoke(entry, [Guid.NewGuid()]);
        typeof(GolferWaitlistEntry).GetProperty(nameof(GolferWaitlistEntry.GroupSize))!
            .GetSetMethod(nonPublic: true)!.Invoke(entry, [2]);

        return entry;
    }
}
