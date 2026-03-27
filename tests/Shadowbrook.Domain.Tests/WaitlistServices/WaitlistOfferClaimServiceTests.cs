using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Domain.Tests.WaitlistServices;

public class WaitlistOfferClaimServiceTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly WaitlistOfferClaimService sut;

    public WaitlistOfferClaimServiceTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.sut = new WaitlistOfferClaimService(this.timeProvider);
    }

    private WaitlistOffer CreateOffer(Guid openingId, int groupSize = 2) =>
        WaitlistOffer.Create(
            openingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            groupSize,
            isWalkUp: true,
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new TimeOnly(9, 0),
            this.timeProvider);

    private TeeTimeOpening CreateOpening(int slotsAvailable = 4) =>
        TeeTimeOpening.Create(
            courseId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            slotsAvailable: slotsAvailable,
            operatorOwned: false,
            timeProvider: this.timeProvider);

    [Fact]
    public void AcceptOffer_WhenClaimSucceeds_ReturnsSuccessResult()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.True(result.Success);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void AcceptOffer_WhenClaimSucceeds_OfferTransitionsToAccepted()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Equal(OfferStatus.Accepted, offer.Status);
    }

    [Fact]
    public void AcceptOffer_WhenClaimSucceeds_OfferRaisesWaitlistOfferAcceptedEvent()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Contains(offer.DomainEvents, e => e is WaitlistOfferAccepted);
    }

    [Fact]
    public void AcceptOffer_WhenClaimSucceeds_OpeningRaisesSlotsClaimed()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningSlotsClaimed);
    }

    [Fact]
    public void AcceptOffer_WhenClaimFails_ReturnsFailureResultWithReason()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = CreateOffer(opening.Id, groupSize: 2); // group too large
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.False(result.Success);
        Assert.Equal("Insufficient slots remaining", result.Reason);
    }

    [Fact]
    public void AcceptOffer_WhenClaimFails_OfferTransitionsToRejectedWithReason()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Insufficient slots remaining", offer.RejectionReason);
    }

    [Fact]
    public void AcceptOffer_WhenClaimFails_OpeningRaisesClaimRejectedEvent()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        this.sut.AcceptOffer(offer, opening);

        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningSlotsClaimRejected);
    }

    [Fact]
    public void AcceptOffer_WhenOpeningNotOpen_ReturnsFailureAndRejectsOffer()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        opening.Expire(this.timeProvider);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.False(result.Success);
        Assert.Equal("Opening is not available", result.Reason);
        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Opening is not available", offer.RejectionReason);
    }

    [Fact]
    public void AcceptOffer_StaleOffer_WhenClaimSucceeds_ReturnsSuccessAndAccepts()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        offer.MarkStale();
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.True(result.Success);
        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Contains(offer.DomainEvents, e => e is WaitlistOfferAccepted);
    }

    [Fact]
    public void AcceptOffer_StaleOffer_WhenClaimFails_ReturnsFailureAndRejects()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        var offer = CreateOffer(opening.Id, groupSize: 2);
        offer.MarkStale();
        opening.ClearDomainEvents();
        offer.ClearDomainEvents();

        var result = this.sut.AcceptOffer(offer, opening);

        Assert.False(result.Success);
        Assert.Equal(OfferStatus.Rejected, offer.Status);
    }
}
