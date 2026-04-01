using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

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
