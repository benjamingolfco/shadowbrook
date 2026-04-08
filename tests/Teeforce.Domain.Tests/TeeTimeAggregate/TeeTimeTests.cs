using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeTimeAggregate;

public class TeeTimeTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly Guid courseId = Guid.NewGuid();
    private readonly DateOnly date = new(2026, 6, 1);

    public TeeTimeTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private (TeeSheet sheet, TeeSheetInterval interval, BookingAuthorization auth) MakeSheetAndInterval(int capacity = 4)
    {
        var settings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, capacity);
        var sheet = TeeSheet.Draft(this.courseId, this.date, settings, this.timeProvider);
        sheet.Publish(this.timeProvider);
        var interval = sheet.Intervals[0];
        var auth = sheet.AuthorizeBooking();
        return (sheet, interval, auth);
    }

    [Fact]
    public void ClaimFactory_CreatesOpenTeeTimeWithFirstClaim()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, golferId, 2, this.timeProvider);

        Assert.Equal(TeeTimeStatus.Open, teeTime.Status);
        Assert.Equal(4, teeTime.Capacity);
        Assert.Equal(2, teeTime.Remaining);
        Assert.Equal(interval.Id, teeTime.TeeSheetIntervalId);
        Assert.Equal(interval.TeeSheetId, teeTime.TeeSheetId);
        Assert.Equal(this.date, teeTime.Date);
        Assert.Equal(interval.Time, teeTime.Time);
        var claim = Assert.Single(teeTime.Claims);
        Assert.Equal(bookingId, claim.BookingId);
        Assert.Equal(2, claim.GroupSize);
    }

    [Fact]
    public void ClaimFactory_RaisesTeeTimeClaimed()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeClaimed);
    }

    [Fact]
    public void ClaimFactory_FullCapacity_TransitionsToFilledAndRaisesFilled()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 2);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Equal(TeeTimeStatus.Filled, teeTime.Status);
        Assert.Equal(0, teeTime.Remaining);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeFilled);
    }

    [Fact]
    public void ClaimInstance_AppendsAndDecrementsRemaining()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Equal(1, teeTime.Remaining);
        Assert.Equal(2, teeTime.Claims.Count);
        Assert.Single(teeTime.DomainEvents.OfType<TeeTimeClaimed>());
    }

    [Fact]
    public void ClaimInstance_OnFilled_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 2);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Throws<TeeTimeFilledException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_OnBlocked_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Block(interval, this.courseId, this.date, "frost", this.timeProvider);

        Assert.Throws<TeeTimeBlockedException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_GroupSizeExceedsRemaining_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 3, this.timeProvider);

        Assert.Throws<InsufficientCapacityException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_NonPositiveGroupSize_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);

        Assert.Throws<InvalidGroupSizeException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 0, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_TokenForOtherSheet_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);

        var otherSheet = TeeSheet.Draft(Guid.NewGuid(), this.date,
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 4), this.timeProvider);
        otherSheet.Publish(this.timeProvider);
        var foreignAuth = otherSheet.AuthorizeBooking();

        Assert.Throws<BookingAuthorizationMismatchException>(() =>
            teeTime.Claim(foreignAuth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
    }

    [Fact]
    public void ReleaseClaim_RemovesClaimAndIncrementsRemaining()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var bookingId = Guid.NewGuid();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.ReleaseClaim(bookingId, this.timeProvider);

        Assert.Empty(teeTime.Claims);
        Assert.Equal(4, teeTime.Remaining);
        var released = Assert.IsType<TeeTimeClaimReleased>(Assert.Single(teeTime.DomainEvents));
        Assert.Equal(bookingId, released.BookingId);
        Assert.Equal(2, released.GroupSize);
    }

    [Fact]
    public void ReleaseClaim_FromFilled_TransitionsBackToOpenAndRaisesReopened()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 2);
        var bookingId = Guid.NewGuid();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.ReleaseClaim(bookingId, this.timeProvider);

        Assert.Equal(TeeTimeStatus.Open, teeTime.Status);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeReopened);
    }

    [Fact]
    public void ReleaseClaim_UnknownBookingId_IsNoOp()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.ReleaseClaim(Guid.NewGuid(), this.timeProvider);

        Assert.Empty(teeTime.DomainEvents);
        Assert.Single(teeTime.Claims);
    }

    [Fact]
    public void BlockFactory_CreatesBlockedTeeTime()
    {
        var (_, interval, _) = MakeSheetAndInterval();
        var teeTime = TeeTime.Block(interval, this.courseId, this.date, "frost", this.timeProvider);

        Assert.Equal(TeeTimeStatus.Blocked, teeTime.Status);
        Assert.Empty(teeTime.Claims);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeBlocked);
    }

    [Fact]
    public void BlockInstance_OpenWithNoClaims_Transitions()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var bookingId = Guid.NewGuid();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, Guid.NewGuid(), 1, this.timeProvider);
        teeTime.ReleaseClaim(bookingId, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Block("maintenance", this.timeProvider);

        Assert.Equal(TeeTimeStatus.Blocked, teeTime.Status);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeBlocked);
    }

    [Fact]
    public void BlockInstance_WithClaims_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);

        Assert.Throws<TeeTimeHasClaimsException>(() => teeTime.Block("frost", this.timeProvider));
    }

    [Fact]
    public void Unblock_Transitions()
    {
        var (_, interval, _) = MakeSheetAndInterval();
        var teeTime = TeeTime.Block(interval, this.courseId, this.date, "frost", this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Unblock(this.timeProvider);

        Assert.Equal(TeeTimeStatus.Open, teeTime.Status);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeUnblocked);
    }

    [Fact]
    public void Unblock_AlreadyOpen_IsNoOp()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Unblock(this.timeProvider);

        Assert.Empty(teeTime.DomainEvents);
    }
}
