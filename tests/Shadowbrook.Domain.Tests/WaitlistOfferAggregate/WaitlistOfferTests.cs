using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.WaitlistOfferAggregate;

public class WaitlistOfferTests
{
    [Fact]
    public void Create_SetsPropertiesAndGeneratesIds()
    {
        var requestId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var offer = WaitlistOffer.Create(requestId, entryId);

        Assert.NotEqual(Guid.Empty, offer.Id);
        Assert.NotEqual(Guid.Empty, offer.Token);
        Assert.NotEqual(Guid.Empty, offer.BookingId);
        Assert.Equal(requestId, offer.TeeTimeRequestId);
        Assert.Equal(entryId, offer.GolferWaitlistEntryId);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Null(offer.RejectionReason);
    }

    [Fact]
    public void Create_RaisesWaitlistOfferCreatedEvent()
    {
        var requestId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var offer = WaitlistOffer.Create(requestId, entryId);

        var domainEvent = Assert.Single(offer.DomainEvents);
        var created = Assert.IsType<WaitlistOfferCreated>(domainEvent);
        Assert.Equal(offer.Id, created.WaitlistOfferId);
        Assert.Equal(requestId, created.TeeTimeRequestId);
        Assert.Equal(entryId, created.GolferWaitlistEntryId);
    }

    [Fact]
    public void Accept_PendingOffer_SetsAcceptedAndRaisesEvent()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offer.ClearDomainEvents();
        var golfer = Golfer.Create("+15558675309", "Jane", "Smith");

        offer.Accept(golfer);

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var accepted = Assert.IsType<WaitlistOfferAccepted>(domainEvent);
        Assert.Equal(offer.Id, accepted.WaitlistOfferId);
        Assert.Equal(offer.BookingId, accepted.BookingId);
        Assert.Equal(offer.TeeTimeRequestId, accepted.TeeTimeRequestId);
        Assert.Equal(offer.GolferWaitlistEntryId, accepted.GolferWaitlistEntryId);
        Assert.Equal(golfer.Id, accepted.GolferId);
    }

    [Fact]
    public void Accept_AlreadyAccepted_ThrowsOfferNotPending()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        var golfer = Golfer.Create("+15558675309", "Jane", "Smith");
        offer.Accept(golfer);

        Assert.Throws<OfferNotPendingException>(() => offer.Accept(golfer));
    }

    [Fact]
    public void Accept_AlreadyRejected_ThrowsOfferNotPending()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        var golfer = Golfer.Create("+15558675309", "Jane", "Smith");
        offer.Reject("test reason");

        Assert.Throws<OfferNotPendingException>(() => offer.Accept(golfer));
    }

    [Fact]
    public void Reject_PendingOffer_SetsRejectedWithReason()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offer.ClearDomainEvents();

        offer.Reject("Tee time has been filled.");

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Tee time has been filled.", offer.RejectionReason);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var rejected = Assert.IsType<WaitlistOfferRejected>(domainEvent);
        Assert.Equal(offer.Id, rejected.WaitlistOfferId);
        Assert.Equal("Tee time has been filled.", rejected.Reason);
    }

    [Fact]
    public void Reject_AlreadyAccepted_NoChange()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        var golfer = Golfer.Create("+15558675309", "Jane", "Smith");
        offer.Accept(golfer);
        offer.ClearDomainEvents();

        offer.Reject("test");

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public void MarkNotified_SetsNotifiedAtAndRaisesEvent()
    {
        var requestId = Guid.NewGuid();
        var offer = WaitlistOffer.Create(requestId, Guid.NewGuid());
        offer.ClearDomainEvents();

        offer.MarkNotified();

        Assert.NotNull(offer.NotifiedAt);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var notified = Assert.IsType<GolferNotifiedOfOffer>(domainEvent);
        Assert.Equal(offer.Id, notified.WaitlistOfferId);
        Assert.Equal(requestId, notified.TeeTimeRequestId);
    }

    [Fact]
    public void MarkNotified_AlreadyNotified_Throws()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offer.MarkNotified();

        Assert.Throws<InvalidOperationException>(() => offer.MarkNotified());
    }
}
