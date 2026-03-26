using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Features.Bookings;

public static class RejectBookingHandler
{
    public static async Task Handle(RejectBookingCommand command, IBookingRepository bookingRepository)
    {
        var booking = await bookingRepository.GetByIdAsync(command.BookingId);
        if (booking is null)
        {
            return;
        }

        booking.RejectBooking();
    }
}
