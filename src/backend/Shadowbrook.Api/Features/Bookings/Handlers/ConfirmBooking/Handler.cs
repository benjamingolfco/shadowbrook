using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class ConfirmBookingHandler
{
    public static async Task Handle(ConfirmBookingCommand command, IBookingRepository bookingRepository)
    {
        var booking = await bookingRepository.GetByIdAsync(command.BookingId);
        if (booking is null)
        {
            return;
        }

        booking.Confirm();
    }
}
