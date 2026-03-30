using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Handlers;

public class FindAndOfferEligibleGolfersHandlerTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly IShortCodeGenerator shortCodeGen = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepo = Substitute.For<ICourseWaitlistRepository>();
    private readonly WaitlistMatchingService matchingService;

    public FindAndOfferEligibleGolfersHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(14, 15));
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(14, 15));
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 3, 25));
        this.timeProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(new DateOnly(2026, 3, 25));
        this.shortCodeGen.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
        this.matchingService = new WaitlistMatchingService(this.entryRepo);
    }

    private async Task<WalkUpGolferWaitlistEntry> CreateEntryAsync(Golfer golfer, int groupSize = 1)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), this.shortCodeGen, this.waitlistRepo, this.timeProvider);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        return await waitlist.Join(golfer, this.entryRepo, this.timeProvider, "UTC", groupSize);
    }

    [Fact]
    public async Task Handle_NoOpening_Throws()
    {
        var openingId = Guid.NewGuid();
        this.openingRepo.GetByIdAsync(openingId).Returns((TeeTimeOpening?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => FindAndOfferEligibleGolfersHandler.Handle(
                new FindAndOfferEligibleGolfers(openingId, 3),
                this.openingRepo, this.matchingService, this.offerRepo,
                this.timeProvider, NullLogger.Instance, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoEligibleEntries_DoesNothing()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.entryRepo.FindEligibleEntriesAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GolferWaitlistEntry>());

        await FindAndOfferEligibleGolfersHandler.Handle(
            new FindAndOfferEligibleGolfers(opening.Id, 3),
            this.openingRepo, this.matchingService, this.offerRepo,
            this.timeProvider, NullLogger.Instance, CancellationToken.None);

        this.offerRepo.DidNotReceive().Add(Arg.Any<WaitlistOffer>());
    }

    [Fact]
    public async Task Handle_EligibleEntries_CreatesOffers()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);

        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var entry = await CreateEntryAsync(golfer);
        this.entryRepo.FindEligibleEntriesAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GolferWaitlistEntry> { entry });

        await FindAndOfferEligibleGolfersHandler.Handle(
            new FindAndOfferEligibleGolfers(opening.Id, 3),
            this.openingRepo, this.matchingService, this.offerRepo,
            this.timeProvider, NullLogger.Instance, CancellationToken.None);

        this.offerRepo.Received(1).Add(Arg.Any<WaitlistOffer>());
    }

    [Fact]
    public async Task Handle_MaxOffersLowerThanEligibleCount_CapsOffersCreated()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);

        var golfer1 = Golfer.Create("+15551111111", "Alice", "Smith");
        var golfer2 = Golfer.Create("+15552222222", "Bob", "Jones");
        var golfer3 = Golfer.Create("+15553333333", "Carol", "White");

        var entry1 = await CreateEntryAsync(golfer1);
        var entry2 = await CreateEntryAsync(golfer2);
        var entry3 = await CreateEntryAsync(golfer3);

        this.entryRepo.FindEligibleEntriesAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GolferWaitlistEntry> { entry1, entry2, entry3 });

        await FindAndOfferEligibleGolfersHandler.Handle(
            new FindAndOfferEligibleGolfers(opening.Id, 1),
            this.openingRepo, this.matchingService, this.offerRepo,
            this.timeProvider, NullLogger.Instance, CancellationToken.None);

        this.offerRepo.Received(1).Add(Arg.Any<WaitlistOffer>());
    }
}
