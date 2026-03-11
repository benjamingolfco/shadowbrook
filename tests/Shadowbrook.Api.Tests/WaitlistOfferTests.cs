using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Api.Tests;

/// <summary>
/// Unit tests for WaitlistOffer.Accept() and WaitlistOffer.Decline() domain behavior.
/// WaitlistOffer lives in the API layer (EF model), so these tests belong here.
/// </summary>
public class WaitlistOfferTests
{
    private static WaitlistOffer CreatePendingOffer()
    {
        var now = DateTimeOffset.UtcNow;
        return new WaitlistOffer
        {
            Id = Guid.NewGuid(),
            TeeTimeRequestId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferPhone = "+15558675309",
            CourseName = "Shadowbrook GC",
            TeeTime = new TimeOnly(10, 0),
            OfferDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = OfferStatus.Pending,
            OfferedAt = now,
            ExpiresAt = now.AddMinutes(5),
            CreatedAt = now
        };
    }

    [Fact]
    public void Accept_WhenPending_SetsAcceptedStatus()
    {
        var offer = CreatePendingOffer();
        var respondedAt = DateTimeOffset.UtcNow;

        offer.Accept(respondedAt);

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Equal(respondedAt, offer.RespondedAt);
    }

    [Fact]
    public void Accept_RaisesWaitlistOfferAccepted_Event()
    {
        var offer = CreatePendingOffer();

        offer.Accept(DateTimeOffset.UtcNow);

        var evt = Assert.Single(offer.DomainEvents.OfType<WaitlistOfferAccepted>());
        Assert.Equal(offer.Id, evt.WaitlistOfferId);
        Assert.Equal(offer.TeeTimeRequestId, evt.TeeTimeRequestId);
        Assert.Equal(offer.GolferWaitlistEntryId, evt.GolferWaitlistEntryId);
        Assert.Equal(offer.GolferPhone, evt.GolferPhone);
        Assert.Equal(offer.CourseName, evt.CourseName);
        Assert.Equal(offer.TeeTime, evt.TeeTime);
        Assert.Equal(offer.OfferDate, evt.OfferDate);
    }

    [Fact]
    public void Accept_WhenNotPending_ThrowsDomainException()
    {
        var offer = CreatePendingOffer();
        offer.Accept(DateTimeOffset.UtcNow);

        Assert.Throws<WaitlistOfferNotPendingException>(() =>
            offer.Accept(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Decline_WhenPending_SetsDeclinedStatus()
    {
        var offer = CreatePendingOffer();
        var respondedAt = DateTimeOffset.UtcNow;

        offer.Decline(respondedAt);

        Assert.Equal(OfferStatus.Declined, offer.Status);
        Assert.Equal(respondedAt, offer.RespondedAt);
    }

    [Fact]
    public void Decline_RaisesWaitlistOfferDeclined_Event()
    {
        var offer = CreatePendingOffer();

        offer.Decline(DateTimeOffset.UtcNow);

        var evt = Assert.Single(offer.DomainEvents.OfType<WaitlistOfferDeclined>());
        Assert.Equal(offer.Id, evt.WaitlistOfferId);
        Assert.Equal(offer.TeeTimeRequestId, evt.TeeTimeRequestId);
        Assert.Equal(offer.GolferWaitlistEntryId, evt.GolferWaitlistEntryId);
        Assert.Equal(offer.GolferPhone, evt.GolferPhone);
    }

    [Fact]
    public void Decline_WhenNotPending_ThrowsDomainException()
    {
        var offer = CreatePendingOffer();
        offer.Decline(DateTimeOffset.UtcNow);

        Assert.Throws<WaitlistOfferNotPendingException>(() =>
            offer.Decline(DateTimeOffset.UtcNow));
    }
}
