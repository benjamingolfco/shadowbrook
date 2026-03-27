using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Features.Waitlist.Policies;
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
            .FirstOrDefaultAsync(w => w.Id == evt.CourseWaitlistId)
            ?? throw new InvalidOperationException($"CourseWaitlist {evt.CourseWaitlistId} not found for event {nameof(GolferJoinedWaitlist)}.");

        // Find any active opening for this course/date
        var dayStart = waitlist.Date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = waitlist.Date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var activeOpening = await db.TeeTimeOpenings
            .FirstOrDefaultAsync(o => o.CourseId == waitlist.CourseId
                && o.TeeTime.Value >= dayStart && o.TeeTime.Value < dayEnd
                && o.Status == TeeTimeOpeningStatus.Open);

        if (activeOpening is null)
        {
            return null;
        }

        return new WakeUpOfferPolicy(activeOpening.Id);
    }
}
