using Microsoft.Extensions.Logging;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public class TeeSheetUnpublishedCancelBookingsHandler(
    ITeeTimeRepository teeTimeRepository,
    IBookingRepository bookingRepository,
    ILogger<TeeSheetUnpublishedCancelBookingsHandler> logger)
{
    public async Task Handle(TeeSheetUnpublished evt, CancellationToken ct)
    {
        var teeTimes = await teeTimeRepository.GetByTeeSheetIdAsync(evt.TeeSheetId, ct);
        if (teeTimes.Count == 0)
        {
            logger.LogInformation("No tee times found for unpublished sheet {TeeSheetId}", evt.TeeSheetId);
            return;
        }

        var teeTimeIds = teeTimes.Select(t => t.Id).ToList();
        var bookings = await bookingRepository.GetByTeeTimeIdsAsync(teeTimeIds, ct);

        foreach (var booking in bookings)
        {
            booking.Cancel(evt.Reason);
        }

        logger.LogInformation(
            "Cancelled {Count} booking(s) for unpublished sheet {TeeSheetId}",
            bookings.Count,
            evt.TeeSheetId);
    }
}
