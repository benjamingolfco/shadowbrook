using Shadowbrook.Api.Features.Waitlist.Policies;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistWakeUpHandler
{
    public static async Task<WakeUpOfferPolicy?> Handle(
        GolferJoinedWaitlist evt,
        ApplicationDbContext db)
    {
        // Find the waitlist to get courseId and date
        var waitlist = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == evt.CourseWaitlistId);

        if (waitlist is null)
        {
            return null;
        }

        // Find any active opening for this course/date
        var activeOpening = await db.TeeTimeOpenings
            .FirstOrDefaultAsync(o => o.CourseId == waitlist.CourseId
                && o.Date == waitlist.Date
                && o.Status == TeeTimeOpeningStatus.Open);

        if (activeOpening is null)
        {
            return null;
        }

        return new WakeUpOfferPolicy(activeOpening.Id);
    }
}
