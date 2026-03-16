using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.WaitlistOfferAggregate;

public class WaitlistOfferTests
{
    [Fact]
    public void Create_SetsPropertiesAndPendingStatus()
    {
        var offer = CreateTestOffer();

        Assert.NotEqual(Guid.Empty, offer.Id);
        Assert.NotEqual(Guid.Empty, offer.Token);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Equal("Test Course", offer.CourseName);
        Assert.Equal("Jane Smith", offer.GolferName);
        Assert.Equal("+15558675309", offer.GolferPhone);
        Assert.Equal(2, offer.GolfersNeeded);
    }

    [Fact]
    public void Accept_PendingOffer_SetsAcceptedAndRaisesEvent()
    {
        var offer = CreateTestOffer();

        offer.Accept(currentAcceptanceCount: 0);

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var accepted = Assert.IsType<WaitlistOfferAccepted>(domainEvent);
        Assert.Equal(offer.Id, accepted.WaitlistOfferId);
        Assert.Equal(1, accepted.AcceptanceCount);
    }

    [Fact]
    public void Accept_AlreadyAccepted_ThrowsOfferNotPending()
    {
        var offer = CreateTestOffer();
        offer.Accept(currentAcceptanceCount: 0);

        Assert.Throws<OfferNotPendingException>(() => offer.Accept(0));
    }

    [Fact]
    public void Accept_ExpiredOffer_ThrowsOfferExpired()
    {
        var offer = CreateTestOffer(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Throws<OfferExpiredException>(() => offer.Accept(0));
        Assert.Equal(OfferStatus.Expired, offer.Status);
    }

    [Fact]
    public void Accept_AllSlotsFilled_ThrowsOfferSlotsFilled()
    {
        var offer = CreateTestOffer(golfersNeeded: 1);

        Assert.Throws<OfferSlotsFilledException>(() => offer.Accept(currentAcceptanceCount: 1));
    }

    [Fact]
    public void Expire_PendingOffer_SetsExpired()
    {
        var offer = CreateTestOffer();

        offer.Expire();

        Assert.Equal(OfferStatus.Expired, offer.Status);
    }

    [Fact]
    public void Expire_AlreadyAccepted_NoChange()
    {
        var offer = CreateTestOffer();
        offer.Accept(0);

        offer.Expire();

        Assert.Equal(OfferStatus.Accepted, offer.Status);
    }

    [Fact]
    public void CheckExpiration_NotExpired_ReturnsFalse()
    {
        var offer = CreateTestOffer();

        Assert.False(offer.CheckExpiration());
        Assert.Equal(OfferStatus.Pending, offer.Status);
    }

    [Fact]
    public void CheckExpiration_Expired_ReturnsTrueAndSetsStatus()
    {
        var offer = CreateTestOffer(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(offer.CheckExpiration());
        Assert.Equal(OfferStatus.Expired, offer.Status);
    }

    private static WaitlistOffer CreateTestOffer(
        int golfersNeeded = 2,
        DateTimeOffset? expiresAt = null)
    {
        return WaitlistOffer.Create(
            teeTimeRequestId: Guid.NewGuid(),
            golferWaitlistEntryId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            courseName: "Test Course",
            date: new DateOnly(2026, 3, 16),
            teeTime: new TimeOnly(10, 0),
            golfersNeeded: golfersNeeded,
            golferName: "Jane Smith",
            golferPhone: "+15558675309",
            expiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15));
    }
}
