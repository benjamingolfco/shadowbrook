using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.BookingAggregate.Exceptions;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate;

public class Booking : Entity
{
    public Guid CourseId { get; private set; }
    public Guid GolferId { get; private set; }
    public TeeTime TeeTime { get; private set; } = null!;
    public string GolferName { get; private set; } = string.Empty;
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
        string golferName,
        int playerCount)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = bookingId,
            CourseId = courseId,
            GolferId = golferId,
            TeeTime = new TeeTime(date, teeTime),
            GolferName = golferName,
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

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
        {
            throw new BookingNotPendingException(Id, Status);
        }

        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmed { BookingId = Id, GolferId = GolferId });
    }

    public void RejectBooking()
    {
        if (Status != BookingStatus.Pending)
        {
            throw new BookingNotPendingException(Id, Status);
        }

        Status = BookingStatus.Rejected;
        AddDomainEvent(new BookingRejected { BookingId = Id, GolferId = GolferId });
    }
}
