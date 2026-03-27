using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.TeeTimeOpeningAggregate;

public class TeeTimeOpeningTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeTimeOpeningTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 3, 25));
    }

    private TeeTimeOpening CreateOpening(int slotsAvailable = 3) =>
        TeeTimeOpening.Create(
            courseId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            slotsAvailable: slotsAvailable,
            operatorOwned: true,
            timeProvider: this.timeProvider);

    [Fact]
    public void Create_SetsPropertiesAndRaisesCreatedEvent()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);
        var teeTime = new TimeOnly(9, 0);

        var opening = TeeTimeOpening.Create(courseId, date, teeTime, slotsAvailable: 4, operatorOwned: true, timeProvider: this.timeProvider);

        Assert.NotEqual(Guid.Empty, opening.Id);
        Assert.Equal(courseId, opening.CourseId);
        Assert.Equal(date, opening.TeeTime.Date);
        Assert.Equal(teeTime, opening.TeeTime.Time);
        Assert.Equal(4, opening.SlotsAvailable);
        Assert.Equal(4, opening.SlotsRemaining);
        Assert.True(opening.OperatorOwned);
        Assert.Equal(TeeTimeOpeningStatus.Open, opening.Status);
        Assert.Null(opening.FilledAt);
        Assert.Null(opening.ExpiredAt);

        var domainEvent = Assert.Single(opening.DomainEvents);
        var created = Assert.IsType<TeeTimeOpeningCreated>(domainEvent);
        Assert.Equal(opening.Id, created.OpeningId);
        Assert.Equal(courseId, created.CourseId);
        Assert.Equal(date, created.Date);
        Assert.Equal(teeTime, created.TeeTime);
        Assert.Equal(4, created.SlotsAvailable);
    }

    [Fact]
    public void TryClaim_WhenSlotsAvailable_DecrementsSlotsRemaining()
    {
        var opening = CreateOpening(slotsAvailable: 3);
        opening.ClearDomainEvents();

        var result = opening.TryClaim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        Assert.True(result.Success);
        Assert.Equal(2, opening.SlotsRemaining);
    }

    [Fact]
    public void TryClaim_WhenSlotsAvailable_RaisesClaimedEvent()
    {
        var opening = CreateOpening(slotsAvailable: 3);
        opening.ClearDomainEvents();
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        opening.TryClaim(bookingId, golferId, groupSize: 1, this.timeProvider);

        var domainEvent = Assert.Single(opening.DomainEvents);
        var claimed = Assert.IsType<TeeTimeOpeningClaimed>(domainEvent);
        Assert.Equal(opening.Id, claimed.OpeningId);
        Assert.Equal(bookingId, claimed.BookingId);
        Assert.Equal(golferId, claimed.GolferId);
        Assert.Equal(opening.CourseId, claimed.CourseId);
        Assert.Equal(opening.TeeTime.Date, claimed.Date);
        Assert.Equal(opening.TeeTime.Time, claimed.TeeTime);
    }

    [Fact]
    public void TryClaim_WhenLastSlot_RaisesFilledEvent()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        opening.ClearDomainEvents();

        opening.TryClaim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        Assert.Equal(2, opening.DomainEvents.Count);
        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningClaimed);
        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningFilled f && f.OpeningId == opening.Id);
        Assert.Equal(TeeTimeOpeningStatus.Filled, opening.Status);
        Assert.NotNull(opening.FilledAt);
    }

    [Fact]
    public void TryClaim_WhenGroupTooLarge_RaisesClaimRejectedEvent()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        opening.ClearDomainEvents();
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var result = opening.TryClaim(bookingId, golferId, groupSize: 2, this.timeProvider);

        Assert.False(result.Success);
        Assert.Equal("Insufficient slots remaining", result.Reason);
        var domainEvent = Assert.Single(opening.DomainEvents);
        var rejected = Assert.IsType<TeeTimeOpeningClaimRejected>(domainEvent);
        Assert.Equal(opening.Id, rejected.OpeningId);
        Assert.Equal(bookingId, rejected.BookingId);
        Assert.Equal(golferId, rejected.GolferId);
        Assert.Equal(1, opening.SlotsRemaining); // unchanged
        Assert.Equal(TeeTimeOpeningStatus.Open, opening.Status);
    }

    [Fact]
    public void TryClaim_WhenAlreadyFilled_ReturnsRejectedAndRaisesEvent()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        opening.TryClaim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);
        opening.ClearDomainEvents();
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var result = opening.TryClaim(bookingId, golferId, groupSize: 1, this.timeProvider);

        Assert.False(result.Success);
        Assert.Equal("Opening is not available", result.Reason);
        var domainEvent = Assert.Single(opening.DomainEvents);
        var rejected = Assert.IsType<TeeTimeOpeningClaimRejected>(domainEvent);
        Assert.Equal(bookingId, rejected.BookingId);
        Assert.Equal(golferId, rejected.GolferId);
    }

    [Fact]
    public void TryClaim_WhenExpired_ReturnsRejectedAndRaisesEvent()
    {
        var opening = CreateOpening();
        opening.Expire(this.timeProvider);
        opening.ClearDomainEvents();
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var result = opening.TryClaim(bookingId, golferId, groupSize: 1, this.timeProvider);

        Assert.False(result.Success);
        Assert.Equal("Opening is not available", result.Reason);
        var domainEvent = Assert.Single(opening.DomainEvents);
        var rejected = Assert.IsType<TeeTimeOpeningClaimRejected>(domainEvent);
        Assert.Equal(bookingId, rejected.BookingId);
        Assert.Equal(golferId, rejected.GolferId);
    }

    [Fact]
    public void Expire_WhenOpen_TransitionsToExpired()
    {
        var opening = CreateOpening();

        opening.Expire(this.timeProvider);

        Assert.Equal(TeeTimeOpeningStatus.Expired, opening.Status);
        Assert.NotNull(opening.ExpiredAt);
    }

    [Fact]
    public void Expire_WhenOpen_RaisesExpiredEvent()
    {
        var opening = CreateOpening();
        opening.ClearDomainEvents();

        opening.Expire(this.timeProvider);

        var domainEvent = Assert.Single(opening.DomainEvents);
        var expired = Assert.IsType<TeeTimeOpeningExpired>(domainEvent);
        Assert.Equal(opening.Id, expired.OpeningId);
    }

    [Fact]
    public void Expire_WhenAlreadyFilled_IsIdempotent()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        opening.TryClaim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);
        opening.ClearDomainEvents();

        opening.Expire(this.timeProvider);

        Assert.Equal(TeeTimeOpeningStatus.Filled, opening.Status);
        Assert.Null(opening.ExpiredAt);
        Assert.Empty(opening.DomainEvents);
    }

    [Fact]
    public void Expire_WhenAlreadyExpired_IsIdempotent()
    {
        var opening = CreateOpening();
        opening.Expire(this.timeProvider);
        opening.ClearDomainEvents();

        opening.Expire(this.timeProvider);

        Assert.Equal(TeeTimeOpeningStatus.Expired, opening.Status);
        Assert.Empty(opening.DomainEvents);
    }

    [Fact]
    public void Create_WhenSlotsAvailableZero_ThrowsInvalidSlotsAvailableException()
    {
        Assert.Throws<InvalidSlotsAvailableException>(() =>
            TeeTimeOpening.Create(
                courseId: Guid.NewGuid(),
                date: new DateOnly(2026, 6, 1),
                teeTime: new TimeOnly(9, 0),
                slotsAvailable: 0,
                operatorOwned: true,
                timeProvider: this.timeProvider));
    }

    [Fact]
    public void TryClaim_WhenGroupSizeZero_ThrowsInvalidGroupSizeException()
    {
        var opening = CreateOpening();
        opening.ClearDomainEvents();

        Assert.Throws<InvalidGroupSizeException>(() =>
            opening.TryClaim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 0, this.timeProvider));
    }

    [Fact]
    public void Claim_WhenSlotsAvailable_AddsClaimedSlot()
    {
        var opening = CreateOpening(slotsAvailable: 3);
        opening.ClearDomainEvents();
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        opening.Claim(bookingId, golferId, groupSize: 2, this.timeProvider);

        var claimedSlot = Assert.Single(opening.ClaimedSlots);
        Assert.Equal(bookingId, claimedSlot.BookingId);
        Assert.Equal(golferId, claimedSlot.GolferId);
        Assert.Equal(2, claimedSlot.GroupSize);
        Assert.Equal(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero), claimedSlot.ClaimedAt);
    }

    [Fact]
    public void Claim_WhenMultipleClaims_AccumulatesClaimedSlots()
    {
        var opening = CreateOpening(slotsAvailable: 4);
        opening.ClearDomainEvents();
        var bookingId1 = Guid.NewGuid();
        var golferId1 = Guid.NewGuid();
        var bookingId2 = Guid.NewGuid();
        var golferId2 = Guid.NewGuid();

        opening.Claim(bookingId1, golferId1, groupSize: 2, this.timeProvider);
        opening.Claim(bookingId2, golferId2, groupSize: 1, this.timeProvider);

        Assert.Equal(2, opening.ClaimedSlots.Count);
        Assert.Contains(opening.ClaimedSlots, cs => cs.BookingId == bookingId1 && cs.GolferId == golferId1 && cs.GroupSize == 2);
        Assert.Contains(opening.ClaimedSlots, cs => cs.BookingId == bookingId2 && cs.GolferId == golferId2 && cs.GroupSize == 1);
    }

    [Fact]
    public void Claim_WhenRejected_DoesNotAddClaimedSlot()
    {
        var opening = CreateOpening(slotsAvailable: 1);
        opening.ClearDomainEvents();

        opening.Claim(Guid.NewGuid(), Guid.NewGuid(), groupSize: 2, this.timeProvider);

        Assert.Empty(opening.ClaimedSlots);
    }
}
