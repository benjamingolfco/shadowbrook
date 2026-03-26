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
        var opening = await openingRepository.GetByCourseTeeTimeAsync(
            evt.CourseId, new TeeTime(evt.Date, evt.TeeTime));

        if (opening is null)
        {
            logger.LogWarning(
                "No tee time opening found for course {CourseId} on {Date} at {TeeTime}, skipping claim",
                evt.CourseId, evt.Date, evt.TeeTime);
            return;
        }

        var result = opening.TryClaim(evt.BookingId, evt.GolferId, evt.GroupSize, timeProvider);

        if (!result.Success)
        {
            logger.LogWarning(
                "Claim rejected for booking {BookingId} on opening {OpeningId}: {Reason}",
                evt.BookingId, opening.Id, result.Reason);
        }
    }
}
