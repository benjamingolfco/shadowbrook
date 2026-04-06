using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistSmsHandler
{
    public static async Task Handle(
        GolferJoinedWaitlist domainEvent,
        INotificationService notificationService,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var courseName = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .Where(w => w.Id == domainEvent.CourseWaitlistId)
            .Join(db.Courses.IgnoreQueryFilters(), w => w.CourseId, c => c.Id, (w, c) => c.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"CourseWaitlist {domainEvent.CourseWaitlistId} or its course not found for event {nameof(GolferJoinedWaitlist)}.");

        var message = $"You're on the waitlist at {courseName}. Keep your phone handy - we'll text you when a spot opens up!";
        await notificationService.Send(domainEvent.GolferId, message, ct);
    }
}
