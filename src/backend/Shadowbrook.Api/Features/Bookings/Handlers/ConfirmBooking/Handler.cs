using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class ConfirmBookingHandler
{
    public static async Task Handle(ConfirmBookingCommand command, IBookingRepository bookingRepository)
    {
        var booking = await bookingRepository.GetRequiredByIdAsync(command.BookingId);

        booking.Confirm();
    }
}
