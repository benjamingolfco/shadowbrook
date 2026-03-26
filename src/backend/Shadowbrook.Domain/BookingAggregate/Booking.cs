using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate;

public class Booking : Entity
{
    public Guid CourseId { get; private set; }
    public Guid GolferId { get; private set; }
    public Guid? OpeningId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
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
        TimeOnly time,
        string golferName,
        int playerCount,
        Guid? openingId = null)
    {
        var status = openingId.HasValue ? BookingStatus.Pending : BookingStatus.Confirmed;
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = bookingId,
            CourseId = courseId,
            GolferId = golferId,
            OpeningId = openingId,
            Date = date,
            Time = time,
            GolferName = golferName,
            PlayerCount = playerCount,
            Status = status,
            CreatedAt = now
        };

        booking.AddDomainEvent(new BookingCreated
        {
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = courseId,
            OpeningId = openingId,
            GroupSize = playerCount
        });

        return booking;
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
        {
            return;
        }

        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmed { BookingId = Id });
    }

    public void RejectBooking()
    {
        if (Status != BookingStatus.Pending)
        {
            return;
        }

        Status = BookingStatus.Rejected;
        AddDomainEvent(new BookingRejected { BookingId = Id });
    }
}
