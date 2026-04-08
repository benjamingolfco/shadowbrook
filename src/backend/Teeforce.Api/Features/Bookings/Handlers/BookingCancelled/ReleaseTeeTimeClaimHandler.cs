using Microsoft.Extensions.Logging;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCancelledReleaseTeeTimeClaimHandler
{
    public static async Task Handle(
        BookingCancelled evt,
        IBookingRepository bookingRepository,
        ITeeTimeRepository teeTimeRepository,
        ITimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(evt.BookingId);
        if (booking is null)
        {
            logger.LogWarning("Booking {BookingId} not found while releasing tee time claim", evt.BookingId);
            return;
        }

        if (booking.TeeTimeId is null)
        {
            // Walk-up bookings flow through TeeTimeOpening — nothing to release on TeeTime side.
            return;
        }

        var teeTime = await teeTimeRepository.GetByIdAsync(booking.TeeTimeId.Value);
        if (teeTime is null)
        {
            logger.LogWarning(
                "TeeTime {TeeTimeId} not found while releasing claim for booking {BookingId}",
                booking.TeeTimeId.Value,
                evt.BookingId);
            return;
        }

        teeTime.ReleaseClaim(evt.BookingId, timeProvider);
    }
}
