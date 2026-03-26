using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistSmsHandler
{
    public static async Task Handle(
        GolferJoinedWaitlist domainEvent,
        ITextMessageService textMessageService,
        IGolferRepository golferRepository,
        IGolferWaitlistEntryRepository entryRepository,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId);
        if (golfer is null)
        {
            return;
        }

        var courseName = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .Where(w => w.Id == domainEvent.CourseWaitlistId)
            .Join(db.Courses.IgnoreQueryFilters(), w => w.CourseId, c => c.Id, (w, c) => c.Name)
            .FirstOrDefaultAsync(ct);

        if (courseName is null)
        {
            return;
        }

        // Calculate position: count active entries that joined at or before this entry.
        var joinedAtValues = await db.GolferWaitlistEntries
            .IgnoreQueryFilters()
            .Where(e => e.CourseWaitlistId == domainEvent.CourseWaitlistId && e.RemovedAt == null)
            .Select(e => new { e.Id, e.JoinedAt })
            .ToListAsync(ct);

        var position = joinedAtValues.Count(e => e.JoinedAt <= entry.JoinedAt);

        var message = $"You're #{position} on the waitlist at {courseName}. Keep your phone handy - we'll text you when a spot opens up!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
