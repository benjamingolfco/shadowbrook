using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public class GolferJoinedWaitlistSmsHandler(
    ITextMessageService textMessageService,
    IGolferRepository golferRepository,
    IGolferWaitlistEntryRepository entryRepository,
    ApplicationDbContext db)
    : IDomainEventHandler<GolferJoinedWaitlist>
{
    public async Task HandleAsync(GolferJoinedWaitlist domainEvent, CancellationToken ct = default)
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

        var courseName = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .Where(w => w.Id == domainEvent.CourseWaitlistId)
            .Join(db.Courses.IgnoreQueryFilters(), w => w.CourseId, c => c.Id, (w, c) => c.Name)
            .FirstOrDefaultAsync(ct);

        if (courseName is null)
        {
            return;
        }

        // Calculate position: count active entries that joined at or before this entry.
        // Fetching JoinedAt values to a list first avoids SQLite DateTimeOffset comparison issues in tests.
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
