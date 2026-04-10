using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class TeeTimeClaimedCreateConfirmedBookingHandler
{
    public static void Handle(
        TeeTimeClaimed evt,
        IBookingRepository bookingRepository)
    {
        var booking = Booking.CreateConfirmed(
            bookingId: evt.BookingId,
            courseId: evt.CourseId,
            golferId: evt.GolferId,
            teeTimeId: evt.TeeTimeId,
            date: evt.Date,
            teeTime: evt.Time,
            playerCount: evt.GroupSize);

        bookingRepository.Add(booking);
    }
}
