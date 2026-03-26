using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class BookingCreatedClaimHandler
{
    public static async Task Handle(
        BookingCreated evt,
        ITeeTimeOpeningRepository openingRepository,
        ITimeProvider timeProvider)
    {
        if (evt.OpeningId is null)
        {
            return; // Not a waitlist booking — no opening to claim
        }

        var opening = await openingRepository.GetRequiredByIdAsync(evt.OpeningId.Value);

        opening.Claim(evt.BookingId, evt.GolferId, evt.GroupSize, timeProvider);
    }
}
