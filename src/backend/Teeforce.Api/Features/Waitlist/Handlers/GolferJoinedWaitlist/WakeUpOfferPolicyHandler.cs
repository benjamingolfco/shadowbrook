using Microsoft.Extensions.Logging;
using Teeforce.Api.Features.Waitlist.Policies;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.CourseWaitlistAggregate.Events;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistServices;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistWakeUpHandler
{
    public static async Task<WakeUpOfferPolicy?> Handle(
        GolferJoinedWaitlist evt,
        ICourseWaitlistRepository waitlistRepository,
        IGolferWaitlistEntryRepository entryRepository,
        WaitlistMatchingService matchingService,
        ILogger logger,
        CancellationToken ct)
    {
        var waitlist = await waitlistRepository.GetRequiredByIdAsync(evt.CourseWaitlistId);

        var entry = await entryRepository.GetByIdAsync(evt.GolferWaitlistEntryId);
        if (entry is null)
        {
            logger.LogWarning(
                "GolferWaitlistEntry {GolferWaitlistEntryId} not found, skipping WakeUpOfferPolicy",
                evt.GolferWaitlistEntryId);
            return null;
        }

        var opening = await matchingService.FindOpeningForGolferAsync(entry, waitlist.CourseId, waitlist.Date, ct);
        if (opening is null)
        {
            return null;
        }

        return new WakeUpOfferPolicy(opening.Id);
    }
}
