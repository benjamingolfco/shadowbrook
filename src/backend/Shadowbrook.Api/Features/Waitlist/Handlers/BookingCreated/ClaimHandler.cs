using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class BookingCreatedClaimHandler
{
    public static async Task Handle(
        BookingCreated evt,
        ITeeTimeOpeningRepository openingRepository,
        ITimeProvider timeProvider,
        ILogger logger)
    {
        if (evt.OpeningId is null)
        {
            return; // Not a waitlist booking — no opening to claim
        }

        var opening = await openingRepository.GetByIdAsync(evt.OpeningId.Value);
        if (opening is null)
        {
            logger.LogWarning("TeeTimeOpening {OpeningId} not found, skipping claim for booking {BookingId}", evt.OpeningId.Value, evt.BookingId);
            return;
        }

        opening.Claim(evt.BookingId, evt.GolferId, evt.GroupSize, timeProvider);
    }
}
