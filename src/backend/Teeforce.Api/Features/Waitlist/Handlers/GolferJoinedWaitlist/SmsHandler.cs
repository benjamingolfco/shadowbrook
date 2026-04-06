using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record GolferJoinedWaitlistNotification(string CourseName) : INotification;

public class GolferJoinedWaitlistNotificationSmsFormatter : SmsFormatter<GolferJoinedWaitlistNotification>
{
    protected override string FormatMessage(GolferJoinedWaitlistNotification n) =>
        $"You're on the waitlist at {n.CourseName}. Keep your phone handy - we'll text you when a spot opens up!";
}

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

        await notificationService.Send(domainEvent.GolferId, new GolferJoinedWaitlistNotification(courseName), ct);
    }
}
