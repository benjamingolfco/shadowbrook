using Microsoft.Extensions.Logging;
using Teeforce.Api.Features.TeeSheet.Policies;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate;
using Teeforce.Domain.WaitlistServices;

namespace Teeforce.Api.Features.TeeSheet.Handlers;

public static class FindAndOfferForTeeTimeHandler
{
    public static async Task Handle(
        FindAndOfferForTeeTime command,
        ITeeTimeWaitlistMatcher matcher,
        ITeeTimeOfferRepository offerRepository,
        ITimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var entries = await matcher.FindEligibleEntries(
            command.TeeTimeId, command.CourseId, command.Date, command.Time,
            command.AvailableSlots, ct);

        if (entries.Count == 0)
        {
            logger.LogInformation(
                "No eligible golfers found for TeeTime {TeeTimeId}, skipping offer dispatch",
                command.TeeTimeId);
            return;
        }

        foreach (var entry in entries.Take(command.AvailableSlots))
        {
            var offer = TeeTimeOffer.Create(
                command.TeeTimeId, entry.Id, entry.GolferId, entry.GroupSize,
                command.CourseId, command.Date, command.Time, timeProvider);
            offerRepository.Add(offer);
        }
    }
}
