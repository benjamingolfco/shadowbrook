using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class RejectBookingHandler
{
    public static async Task Handle(RejectBookingCommand command, IBookingRepository bookingRepository)
    {
        var booking = await bookingRepository.GetRequiredByIdAsync(command.BookingId);

        booking.RejectBooking();
    }
}
