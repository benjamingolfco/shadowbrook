using Microsoft.Extensions.Logging;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate;

namespace Teeforce.Api.Features.TeeSheet.Handlers;

public static class MarkOfferAcceptedHandler
{
    public static async Task Handle(
        BookingConfirmed evt,
        IBookingRepository bookingRepository,
        ITeeTimeOfferRepository offerRepository,
        ILogger logger,
        CancellationToken ct)
    {
        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        if (booking.TeeTimeId is null)
        {
            return; // not a tee-time booking — nothing to correlate
        }

        var offer = await offerRepository.GetPendingByTeeTimeAndGolfer(
            booking.TeeTimeId.Value, booking.GolferId, ct);

        if (offer is null)
        {
            logger.LogInformation(
                "No pending TeeTimeOffer for TeeTime {TeeTimeId} and Golfer {GolferId}, skipping (direct booking)",
                booking.TeeTimeId, booking.GolferId);
            return;
        }

        offer.MarkAccepted(evt.BookingId);
    }
}
