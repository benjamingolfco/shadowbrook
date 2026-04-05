using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;
using Teeforce.Domain.WaitlistServices;

namespace Teeforce.Domain.Tests.WaitlistServices;

public class WaitlistOfferClaimServiceTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepository = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly WaitlistOfferClaimService sut;

    public WaitlistOfferClaimServiceTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(9, 0));
        this.timeProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(new DateOnly(2026, 6, 1));
        this.sut = new WaitlistOfferClaimService(this.timeProvider);
    }

    private async Task<WaitlistOffer> CreateOfferAsync(TeeTimeOpening opening, int groupSize = 2)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 6, 1),
            this.shortCodeGenerator, this.waitlistRepository, this.timeProvider);

        var golfer = Golfer.Create("+15551234567", "Test", "Golfer");
        var entry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC", groupSize);
        return entry.CreateOffer(opening, this.timeProvider);
    }

    private TeeTimeOpening CreateOpening(int slotsAvailable = 4) =>
        TeeTimeOpening.Create(
            courseId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            slotsAvailable: slotsAvailable,
            operatorOwned: false,
            timeProvider: this.timeProvider);

    [Fact]
    public async Task AcceptOffer_WhenClaimSucceeds_ReturnsSuccessResult()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.True(result.Success);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task AcceptOffer_WhenClaimSucceeds_OfferTransitionsToAccepted()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Equal(OfferStatus.Accepted, offer.Status);
    }

    [Fact]
    public async Task AcceptOffer_WhenClaimSucceeds_OfferRaisesWaitlistOfferAcceptedEvent()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Contains(offer.DomainEvents, e => e is WaitlistOfferAccepted);
    }

    [Fact]
    public async Task AcceptOffer_WhenClaimSucceeds_OpeningRaisesSlotsClaimed()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningSlotsClaimed);
    }

    [Fact]
    public async Task AcceptOffer_WhenClaimFails_ReturnsFailureResultWithReason()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = await CreateOfferAsync(opening, groupSize: 2); // group too large
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.False(result.Success);
        Assert.Equal("Insufficient slots remaining", result.Reason);
    }

    [Fact]
    public async Task AcceptOffer_WhenClaimFails_OfferTransitionsToRejectedWithReason()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Insufficient slots remaining", offer.RejectionReason);
    }

    [Fact]
    public async Task AcceptOffer_WhenClaimFails_OpeningRaisesClaimRejectedEvent()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningSlotsClaimRejected);
    }

    [Fact]
    public async Task AcceptOffer_WhenOpeningNotOpen_ReturnsFailureAndRejectsOffer()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        opening.Expire(this.timeProvider);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.False(result.Success);
        Assert.Equal("Opening is not available", result.Reason);
        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Opening is not available", offer.RejectionReason);
    }

    [Fact]
    public async Task AcceptOffer_StaleOffer_WhenClaimSucceeds_ReturnsSuccessAndAccepts()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        offer.MarkStale();
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Contains(offer.DomainEvents, e => e is WaitlistOfferAccepted);
    }

    [Fact]
    public async Task AcceptOffer_StaleOffer_WhenClaimFails_ReturnsFailureAndRejects()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = await CreateOfferAsync(opening, groupSize: 2);
        offer.MarkStale();
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.False(result.Success);
        Assert.Equal(OfferStatus.Rejected, offer.Status);
    }
}
