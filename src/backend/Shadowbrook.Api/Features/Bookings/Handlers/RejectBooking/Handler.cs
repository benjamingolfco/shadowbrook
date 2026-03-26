using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class RejectBookingHandler
{
    public static async Task Handle(RejectBookingCommand command, IBookingRepository bookingRepository)
    {
        var booking = await bookingRepository.GetByIdAsync(command.BookingId)
            ?? throw new InvalidOperationException($"Booking {command.BookingId} not found for command {nameof(RejectBookingCommand)}.");

        booking.RejectBooking();
    }
}
