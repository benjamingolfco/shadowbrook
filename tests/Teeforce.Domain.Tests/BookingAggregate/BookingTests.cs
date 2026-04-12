using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.BookingAggregate.Exceptions;

namespace Teeforce.Domain.Tests.BookingAggregate;

public class BookingTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var bookingId = Guid.CreateVersion7();
        var courseId = Guid.NewGuid();
        var golferId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(9, 0);
        var playerCount = 2;

        var booking = Booking.Create(bookingId, courseId, golferId, date, time, playerCount);

        Assert.Equal(bookingId, booking.Id);
        Assert.Equal(courseId, booking.CourseId);
        Assert.Equal(golferId, booking.GolferId);
        Assert.Equal(date, booking.TeeTime.Date);
        Assert.Equal(time, booking.TeeTime.Time);
        Assert.Equal(playerCount, booking.PlayerCount);
        Assert.NotEqual(default, booking.CreatedAt);
    }

    [Fact]
    public void Create_AlwaysStartsAsPending()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            2);

        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void Create_UsesPreAllocatedBookingId()
    {
        var bookingId = Guid.CreateVersion7();

        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        Assert.Equal(bookingId, booking.Id);
    }

    [Fact]
    public void Create_RaisesBookingCreatedEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var courseId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var booking = Booking.Create(
            bookingId,
            courseId,
            golferId,
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        var domainEvent = Assert.Single(booking.DomainEvents);
        var createdEvent = Assert.IsType<BookingCreated>(domainEvent);
        Assert.Equal(bookingId, createdEvent.BookingId);
        Assert.Equal(golferId, createdEvent.GolferId);
        Assert.Equal(courseId, createdEvent.CourseId);
    }

    [Fact]
    public void Create_EventCarriesDateTeeTimeAndGroupSize()
    {
        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(9, 0);
        var playerCount = 3;

        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            date,
            time,
            playerCount);

        var domainEvent = Assert.Single(booking.DomainEvents);
        var createdEvent = Assert.IsType<BookingCreated>(domainEvent);
        Assert.Equal(date, createdEvent.Date);
        Assert.Equal(time, createdEvent.TeeTime);
        Assert.Equal(playerCount, createdEvent.GroupSize);
    }

    [Fact]
    public void CreateConfirmed_SetsStatusToConfirmed()
    {
        var bookingId = Guid.CreateVersion7();

        var booking = Booking.CreateConfirmed(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            teeTimeId: null,
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            2);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void CreateConfirmed_SetsAllProperties()
    {
        var bookingId = Guid.CreateVersion7();
        var courseId = Guid.NewGuid();
        var golferId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(9, 0);
        var playerCount = 2;

        var booking = Booking.CreateConfirmed(bookingId, courseId, golferId, teeTimeId: null, date, time, playerCount);

        Assert.Equal(bookingId, booking.Id);
        Assert.Equal(courseId, booking.CourseId);
        Assert.Equal(golferId, booking.GolferId);
        Assert.Equal(date, booking.TeeTime.Date);
        Assert.Equal(time, booking.TeeTime.Time);
        Assert.Equal(playerCount, booking.PlayerCount);
        Assert.NotEqual(default, booking.CreatedAt);
    }

    [Fact]
    public void CreateConfirmed_RaisesBookingConfirmedEvent()
    {
        var bookingId = Guid.CreateVersion7();

        var booking = Booking.CreateConfirmed(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            teeTimeId: null,
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        var domainEvent = Assert.Single(booking.DomainEvents);
        var confirmedEvent = Assert.IsType<BookingConfirmed>(domainEvent);
        Assert.Equal(bookingId, confirmedEvent.BookingId);
    }

    [Fact]
    public void CreateConfirmed_UsesPreAllocatedBookingId()
    {
        var bookingId = Guid.CreateVersion7();

        var booking = Booking.CreateConfirmed(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            teeTimeId: null,
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        Assert.Equal(bookingId, booking.Id);
    }

    [Fact]
    public void CreateConfirmed_StoresTeeTimeId()
    {
        var teeTimeId = Guid.NewGuid();
        var booking = Booking.CreateConfirmed(
            bookingId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            teeTimeId: teeTimeId,
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            playerCount: 2);

        Assert.Equal(teeTimeId, booking.TeeTimeId);
    }

    [Fact]
    public void CreateConfirmed_AllowsNullTeeTimeId_ForCompatSeam()
    {
        var booking = Booking.CreateConfirmed(
            bookingId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            teeTimeId: null,
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            playerCount: 2);

        Assert.Null(booking.TeeTimeId);
    }

    [Fact]
    public void Confirm_FromPending_TransitionsToConfirmedAndRaisesEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Confirm();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        var confirmedEvent = Assert.IsType<BookingConfirmed>(booking.DomainEvents.Last());
        Assert.Equal(bookingId, confirmedEvent.BookingId);
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_IsIdempotent()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Confirm();
        var eventsAfterFirst = booking.DomainEvents.Count;

        booking.Confirm(); // idempotent — no state change, no new event

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(eventsAfterFirst, booking.DomainEvents.Count);
    }

    [Fact]
    public void Confirm_WhenRejected_ThrowsBookingNotPendingException()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Reject();

        Assert.Throws<BookingNotPendingException>(() => booking.Confirm());
    }

    [Fact]
    public void Reject_FromPending_TransitionsToRejectedAndRaisesEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Reject();

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        var rejectedEvent = Assert.IsType<BookingRejected>(booking.DomainEvents.Last());
        Assert.Equal(bookingId, rejectedEvent.BookingId);
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_IsIdempotent()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Reject();
        var eventsAfterFirst = booking.DomainEvents.Count;

        booking.Reject(); // idempotent — no state change, no new event

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(eventsAfterFirst, booking.DomainEvents.Count);
    }

    [Fact]
    public void Reject_WhenConfirmed_ThrowsBookingNotPendingException()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Confirm();

        Assert.Throws<BookingNotPendingException>(() => booking.Reject());
    }

    [Fact]
    public void Cancel_FromPending_TransitionsToCancelledAndRaisesEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
        Assert.Equal(bookingId, cancelledEvent.BookingId);
    }

    [Fact]
    public void Cancel_FromConfirmed_TransitionsToCancelledAndRaisesEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Confirm();
        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
        Assert.Equal(bookingId, cancelledEvent.BookingId);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_IsIdempotent()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Cancel();
        var eventsAfterFirst = booking.DomainEvents.Count;

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(eventsAfterFirst, booking.DomainEvents.Count);
    }

    [Fact]
    public void Cancel_WhenRejected_ThrowsBookingNotCancellableException()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Reject();

        Assert.Throws<BookingNotCancellableException>(() => booking.Cancel());
    }

    [Fact]
    public void Cancel_FromPending_EventCarriesPendingAsPreviousStatus()
    {
        var bookingId = Guid.CreateVersion7();
        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Cancel();

        var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
        Assert.Equal(bookingId, cancelledEvent.BookingId);
        Assert.Equal(BookingStatus.Pending, cancelledEvent.PreviousStatus);
    }

    [Fact]
    public void Cancel_FromConfirmed_EventCarriesConfirmedAsPreviousStatus()
    {
        var bookingId = Guid.CreateVersion7();
        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Confirm();
        booking.Cancel();

        var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
        Assert.Equal(bookingId, cancelledEvent.BookingId);
        Assert.Equal(BookingStatus.Confirmed, cancelledEvent.PreviousStatus);
    }

    [Fact]
    public void Cancel_WithReason_EventCarriesReason()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Confirm();
        booking.Cancel("Course maintenance");

        var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
        Assert.Equal("Course maintenance", cancelledEvent.Reason);
    }

    [Fact]
    public void Cancel_WithoutReason_EventReasonIsNull()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            1);

        booking.Cancel();

        var cancelledEvent = Assert.IsType<BookingCancelled>(booking.DomainEvents.Last());
        Assert.Null(cancelledEvent.Reason);
    }
}
