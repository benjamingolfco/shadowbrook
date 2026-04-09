using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate;
using Teeforce.Domain.TeeTimeOfferAggregate.Events;
using Teeforce.Domain.TeeTimeOfferAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeTimeOfferAggregate;

public class TeeTimeOfferTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly DateTimeOffset now = new(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid teeTimeId = Guid.NewGuid();
    private readonly Guid entryId = Guid.NewGuid();
    private readonly Guid golferId = Guid.NewGuid();
    private readonly Guid courseId = Guid.NewGuid();
    private readonly DateOnly date = new(2026, 6, 1);
    private readonly TimeOnly time = new(9, 0);

    public TeeTimeOfferTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(this.now);
    }

    private TeeTimeOffer CreateOffer(int groupSize = 2) =>
        TeeTimeOffer.Create(
            this.teeTimeId, this.entryId, this.golferId, groupSize,
            this.courseId, this.date, this.time, this.timeProvider);

    [Fact]
    public void Create_SetsProperties()
    {
        var offer = CreateOffer(groupSize: 2);

        Assert.NotEqual(Guid.Empty, offer.Id);
        Assert.Equal(this.teeTimeId, offer.TeeTimeId);
        Assert.Equal(this.entryId, offer.GolferWaitlistEntryId);
        Assert.Equal(this.golferId, offer.GolferId);
        Assert.Equal(2, offer.GroupSize);
        Assert.NotEqual(Guid.Empty, offer.Token);
        Assert.Equal(this.courseId, offer.CourseId);
        Assert.Equal(this.date, offer.Date);
        Assert.Equal(this.time, offer.Time);
        Assert.Equal(TeeTimeOfferStatus.Pending, offer.Status);
        Assert.Null(offer.RejectionReason);
        Assert.False(offer.IsStale);
        Assert.Equal(this.now, offer.CreatedAt);
        Assert.Null(offer.NotifiedAt);
    }

    [Fact]
    public void Create_RaisesOfferCreated()
    {
        var offer = CreateOffer();

        var evt = Assert.Single(offer.DomainEvents.OfType<TeeTimeOfferCreated>());
        Assert.Equal(offer.Id, evt.TeeTimeOfferId);
        Assert.Equal(this.teeTimeId, evt.TeeTimeId);
        Assert.Equal(this.golferId, evt.GolferId);
        Assert.Equal(2, evt.GroupSize);
        Assert.Equal(this.courseId, evt.CourseId);
        Assert.Equal(this.date, evt.Date);
        Assert.Equal(this.time, evt.Time);
    }

    [Fact]
    public void MarkNotified_SetsNotifiedAtAndRaisesEvent()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();

        offer.MarkNotified(this.timeProvider);

        Assert.Equal(this.now, offer.NotifiedAt);
        var evt = Assert.Single(offer.DomainEvents.OfType<TeeTimeOfferSent>());
        Assert.Equal(offer.Id, evt.TeeTimeOfferId);
        Assert.Equal(this.teeTimeId, evt.TeeTimeId);
        Assert.Equal(this.golferId, evt.GolferId);
        Assert.Equal(2, evt.GroupSize);
    }

    [Fact]
    public void MarkNotified_WhenAlreadyNotified_Throws()
    {
        var offer = CreateOffer();
        offer.MarkNotified(this.timeProvider);

        Assert.Throws<OfferAlreadyNotifiedException>(() => offer.MarkNotified(this.timeProvider));
    }

    [Fact]
    public void MarkAccepted_SetsStatusAndRaisesEvent()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();
        var bookingId = Guid.NewGuid();

        offer.MarkAccepted(bookingId);

        Assert.Equal(TeeTimeOfferStatus.Accepted, offer.Status);
        var evt = Assert.Single(offer.DomainEvents.OfType<TeeTimeOfferAccepted>());
        Assert.Equal(offer.Id, evt.TeeTimeOfferId);
        Assert.Equal(this.teeTimeId, evt.TeeTimeId);
        Assert.Equal(bookingId, evt.BookingId);
        Assert.Equal(this.golferId, evt.GolferId);
        Assert.Equal(2, evt.GroupSize);
        Assert.Equal(this.courseId, evt.CourseId);
        Assert.Equal(this.date, evt.Date);
        Assert.Equal(this.time, evt.Time);
    }

    [Fact]
    public void MarkAccepted_WhenNotPending_Throws()
    {
        var offer = CreateOffer();
        offer.Reject("test");

        Assert.Throws<OfferNotPendingException>(() => offer.MarkAccepted(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_SetsStatusAndReasonAndRaisesEvent()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();

        offer.Reject("golfer declined");

        Assert.Equal(TeeTimeOfferStatus.Rejected, offer.Status);
        Assert.Equal("golfer declined", offer.RejectionReason);
        var evt = Assert.Single(offer.DomainEvents.OfType<TeeTimeOfferRejected>());
        Assert.Equal(offer.Id, evt.TeeTimeOfferId);
        Assert.Equal(this.teeTimeId, evt.TeeTimeId);
        Assert.Equal("golfer declined", evt.Reason);
    }

    [Fact]
    public void Reject_WhenAlreadyResolved_IsIdempotent()
    {
        var offer = CreateOffer();
        offer.Reject("first");
        offer.ClearDomainEvents();

        offer.Reject("second");

        Assert.Empty(offer.DomainEvents);
        Assert.Equal("first", offer.RejectionReason);
    }

    [Fact]
    public void Expire_SetsStatusAndRaisesEvent()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();

        offer.Expire();

        Assert.Equal(TeeTimeOfferStatus.Expired, offer.Status);
        var evt = Assert.Single(offer.DomainEvents.OfType<TeeTimeOfferExpired>());
        Assert.Equal(offer.Id, evt.TeeTimeOfferId);
        Assert.Equal(this.teeTimeId, evt.TeeTimeId);
    }

    [Fact]
    public void Expire_WhenAlreadyResolved_IsIdempotent()
    {
        var offer = CreateOffer();
        offer.MarkAccepted(Guid.NewGuid());
        offer.ClearDomainEvents();

        offer.Expire();

        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public void MarkStale_SetsIsStaleAndRaisesEvent()
    {
        var offer = CreateOffer();
        offer.ClearDomainEvents();

        offer.MarkStale();

        Assert.True(offer.IsStale);
        var evt = Assert.Single(offer.DomainEvents.OfType<TeeTimeOfferStale>());
        Assert.Equal(offer.Id, evt.TeeTimeOfferId);
        Assert.Equal(this.teeTimeId, evt.TeeTimeId);
    }

    [Fact]
    public void MarkStale_WhenAlreadyStale_IsIdempotent()
    {
        var offer = CreateOffer();
        offer.MarkStale();
        offer.ClearDomainEvents();

        offer.MarkStale();

        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public void MarkStale_WhenAlreadyResolved_IsIdempotent()
    {
        var offer = CreateOffer();
        offer.Reject("done");
        offer.ClearDomainEvents();

        offer.MarkStale();

        Assert.Empty(offer.DomainEvents);
    }
}
