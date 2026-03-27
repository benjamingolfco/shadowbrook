using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandler
{
    public static void Handle(
        TeeTimeOpeningSlotsClaimed evt,
        IBookingRepository bookingRepository)
    {
        var booking = Booking.CreateConfirmed(
            bookingId: evt.BookingId,
            courseId: evt.CourseId,
            golferId: evt.GolferId,
            date: evt.Date,
            teeTime: evt.TeeTime,
            playerCount: evt.GroupSize);

        bookingRepository.Add(booking);
    }
}
