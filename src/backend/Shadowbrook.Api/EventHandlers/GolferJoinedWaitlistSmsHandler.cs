using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public class GolferJoinedWaitlistSmsHandler(
    ITextMessageService textMessageService,
    ApplicationDbContext db)
    : IDomainEventHandler<GolferJoinedWaitlist>
{
    public async Task HandleAsync(GolferJoinedWaitlist domainEvent, CancellationToken ct = default)
    {
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        var message = $"You're #{domainEvent.Position} on the waitlist at {courseName}. Keep your phone handy - we'll text you when a spot opens up!";
        await textMessageService.SendAsync(domainEvent.GolferPhone, message, ct);
    }
}
