using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistSmsHandler
{
    public static async Task Handle(
        GolferJoinedWaitlist domainEvent,
        ITextMessageService textMessageService,
        IGolferRepository golferRepository,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var golfer = await golferRepository.GetRequiredByIdAsync(domainEvent.GolferId);

        var courseName = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .Where(w => w.Id == domainEvent.CourseWaitlistId)
            .Join(db.Courses.IgnoreQueryFilters(), w => w.CourseId, c => c.Id, (w, c) => c.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"CourseWaitlist {domainEvent.CourseWaitlistId} or its course not found for event {nameof(GolferJoinedWaitlist)}.");

        var message = $"You're on the waitlist at {courseName}. Keep your phone handy - we'll text you when a spot opens up!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
