using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class RejectBookingHandler
{
    public static async Task Handle(RejectBookingCommand command, IBookingRepository bookingRepository, ILogger logger)
    {
        var booking = await bookingRepository.GetByIdAsync(command.BookingId);
        if (booking is null)
        {
            logger.LogWarning("Booking {BookingId} not found, skipping reject", command.BookingId);
            return;
        }

        booking.RejectBooking();
    }
}
