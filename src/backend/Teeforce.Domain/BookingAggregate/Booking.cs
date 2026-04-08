using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.BookingAggregate.Exceptions;
using Teeforce.Domain.Common;

namespace Teeforce.Domain.BookingAggregate;

public class Booking : Entity
{
    public Guid CourseId { get; private set; }
    public Guid GolferId { get; private set; }
    public BookingDateTime TeeTime { get; private set; } = null!;
    public int PlayerCount { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Booking() { } // EF

    public static Booking Create(
        Guid bookingId,
        Guid courseId,
        Guid golferId,
        DateOnly date,
        TimeOnly teeTime,
        int playerCount)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = bookingId,
            CourseId = courseId,
            GolferId = golferId,
            TeeTime = new BookingDateTime(date, teeTime),
            PlayerCount = playerCount,
            Status = BookingStatus.Pending,
            CreatedAt = now
        };

        booking.AddDomainEvent(new BookingCreated
        {
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime,
            GroupSize = playerCount
        });

        return booking;
    }

    public static Booking CreateConfirmed(
        Guid bookingId,
        Guid courseId,
        Guid golferId,
        DateOnly date,
        TimeOnly teeTime,
        int playerCount)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = bookingId,
            CourseId = courseId,
            GolferId = golferId,
            TeeTime = new BookingDateTime(date, teeTime),
            PlayerCount = playerCount,
            Status = BookingStatus.Confirmed,
            CreatedAt = now
        };

        booking.AddDomainEvent(new BookingConfirmed { BookingId = bookingId });

        return booking;
    }

    public void Confirm()
    {
        if (Status == BookingStatus.Confirmed)
        {
            return;
        }

        if (Status != BookingStatus.Pending)
        {
            throw new BookingNotPendingException(Id, Status);
        }

        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmed { BookingId = Id });
    }

    public void Reject()
    {
        if (Status == BookingStatus.Rejected)
        {
            return;
        }

        if (Status != BookingStatus.Pending)
        {
            throw new BookingNotPendingException(Id, Status);
        }

        Status = BookingStatus.Rejected;
        AddDomainEvent(new BookingRejected { BookingId = Id });
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled)
        {
            return;
        }

        if (Status == BookingStatus.Rejected)
        {
            throw new BookingNotCancellableException(Id, Status);
        }

        var previousStatus = Status;
        Status = BookingStatus.Cancelled;
        AddDomainEvent(new BookingCancelled { BookingId = Id, PreviousStatus = previousStatus });
    }
}
