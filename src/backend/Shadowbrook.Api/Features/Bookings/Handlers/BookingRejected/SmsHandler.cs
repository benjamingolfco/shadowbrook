using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class BookingRejectedSmsHandler
{
    public static async Task Handle(
        BookingRejected evt,
        IGolferRepository golferRepository,
        ITextMessageService smsService,
        ILogger logger,
        CancellationToken ct)
    {
        var golfer = await golferRepository.GetByIdAsync(evt.GolferId);
        if (golfer is null)
        {
            logger.LogWarning(
                "Golfer {GolferId} not found for booking rejection SMS",
                evt.GolferId);
            return;
        }

        await smsService.SendAsync(
            golfer.Phone,
            "Sorry, that tee time was claimed by another golfer. You're still on the waitlist for future openings!",
            ct);
    }
}
