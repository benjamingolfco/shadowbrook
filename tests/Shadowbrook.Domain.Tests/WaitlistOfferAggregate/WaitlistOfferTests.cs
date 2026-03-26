using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.WaitlistOfferAggregate;

public class WaitlistOfferTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public WaitlistOfferTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private WaitlistOffer CreateOffer(Guid? openingId = null) =>
        WaitlistOffer.Create(
            openingId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, true, this.timeProvider);

    [Fact]
    public void Create_SetsPropertiesAndGeneratesIds()
    {
        var openingId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var offer = WaitlistOffer.Create(openingId, entryId, golferId, 2, true, this.timeProvider);

        Assert.NotEqual(Guid.Empty, offer.Id);
        Assert.NotEqual(Guid.Empty, offer.Token);
        Assert.Equal(openingId, offer.OpeningId);
        Assert.Equal(entryId, offer.GolferWaitlistEntryId);
        Assert.Equal(golferId, offer.GolferId);
        Assert.Equal(2, offer.GroupSize);
        Assert.True(offer.IsWalkUp);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Null(offer.RejectionReason);
    }

    [Fact]
    public void Create_RaisesWaitlistOfferCreatedEvent()
    {
        var openingId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var offer = WaitlistOffer.Create(openingId, entryId, golferId, 2, true, this.timeProvider);

        var domainEvent = Assert.Single(offer.DomainEvents);
        var created = Assert.IsType<WaitlistOfferCreated>(domainEvent);
        Assert.Equal(offer.Id, created.WaitlistOfferId);
        Assert.Equal(openingId, created.OpeningId);
        Assert.Equal(entryId, created.GolferWaitlistEntryId);
        Assert.Equal(golferId, created.GolferId);
        Assert.Equal(2, created.GroupSize);
        Assert.True(created.IsWalkUp);
    }

    [Fact]
    public void Accept_PendingOffer_SetsAcceptedAndRaisesEvent()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();

        offer.Accept();

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var accepted = Assert.IsType<WaitlistOfferAccepted>(domainEvent);
        Assert.Equal(offer.Id, accepted.WaitlistOfferId);
        Assert.Equal(offer.OpeningId, accepted.OpeningId);
        Assert.Equal(offer.GolferWaitlistEntryId, accepted.GolferWaitlistEntryId);
        Assert.Equal(offer.GolferId, accepted.GolferId);
        Assert.Equal(offer.GroupSize, accepted.GroupSize);
    }

    [Fact]
    public void Accept_AlreadyAccepted_ThrowsOfferNotPending()
    {
        var offer = CreateOffer();
        offer.Accept();

        Assert.Throws<OfferNotPendingException>(() => offer.Accept());
    }

    [Fact]
    public void Accept_AlreadyRejected_ThrowsOfferNotPending()
    {
        var offer = CreateOffer();
        offer.Reject("test reason");

        Assert.Throws<OfferNotPendingException>(() => offer.Accept());
    }

    [Fact]
    public void Reject_PendingOffer_SetsRejectedWithReason()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();

        offer.Reject("Tee time has been filled.");

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Tee time has been filled.", offer.RejectionReason);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var rejected = Assert.IsType<WaitlistOfferRejected>(domainEvent);
        Assert.Equal(offer.Id, rejected.WaitlistOfferId);
        Assert.Equal(offer.OpeningId, rejected.OpeningId);
        Assert.Equal("Tee time has been filled.", rejected.Reason);
    }

    [Fact]
    public void Reject_AlreadyAccepted_NoChange()
    {
        var offer = CreateOffer();
        offer.Accept();
        offer.ClearDomainEvents();

        offer.Reject("test");

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public void MarkNotified_SetsNotifiedAtAndRaisesEvent()
    {
        var openingId = Guid.NewGuid();
        var offer = CreateOffer(openingId);
        offer.ClearDomainEvents();

        offer.MarkNotified();

        Assert.NotNull(offer.NotifiedAt);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var sent = Assert.IsType<WaitlistOfferSent>(domainEvent);
        Assert.Equal(offer.Id, sent.WaitlistOfferId);
        Assert.Equal(openingId, sent.OpeningId);
    }

    [Fact]
    public void MarkNotified_AlreadyNotified_Throws()
    {
        var offer = CreateOffer();
        offer.MarkNotified();

        Assert.Throws<InvalidOperationException>(() => offer.MarkNotified());
    }
}
