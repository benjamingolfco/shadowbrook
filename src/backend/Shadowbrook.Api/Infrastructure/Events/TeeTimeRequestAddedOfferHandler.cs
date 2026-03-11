using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Events;

namespace Shadowbrook.Api.Infrastructure.Events;

public class TeeTimeRequestAddedOfferHandler(
    ApplicationDbContext db,
    ITextMessageService textMessageService,
    TimeProvider timeProvider,
    ILogger<TeeTimeRequestAddedOfferHandler> logger)
    : IDomainEventHandler<TeeTimeRequestAdded>
{
    private const int ResponseWindowMinutes = 5;

    public async Task HandleAsync(TeeTimeRequestAdded domainEvent, CancellationToken ct = default)
    {
        // Find the first eligible golfer in the queue (FIFO, active, ready)
        // Load all eligible entries client-side and sort in memory — SQLite does not support
        // DateTimeOffset ordering in EF Core queries.
        var eligibleEntries = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == domainEvent.WaitlistId
                        && e.RemovedAt == null
                        && e.IsReady)
            .ToListAsync(ct);

        var firstEntry = eligibleEntries
            .OrderBy(e => e.JoinedAt)
            .FirstOrDefault();

        if (firstEntry is null)
        {
            logger.LogInformation(
                "No eligible golfers in queue for waitlist {WaitlistId} (request {TeeTimeRequestId}). No SMS sent.",
                domainEvent.WaitlistId, domainEvent.TeeTimeRequestId);
            return;
        }

        var courseName = await db.Courses
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        var now = timeProvider.GetUtcNow();
        var offer = new WaitlistOffer
        {
            Id = Guid.CreateVersion7(),
            TeeTimeRequestId = domainEvent.TeeTimeRequestId,
            GolferWaitlistEntryId = firstEntry.Id,
            GolferPhone = firstEntry.GolferPhone,
            CourseName = courseName,
            TeeTime = domainEvent.TeeTime,
            OfferDate = domainEvent.Date,
            Status = OfferStatus.Pending,
            ResponseWindowMinutes = ResponseWindowMinutes,
            OfferedAt = now,
            ExpiresAt = now.AddMinutes(ResponseWindowMinutes),
            CreatedAt = now
        };

        db.WaitlistOffers.Add(offer);
        await db.SaveChangesAsync(ct);

        var teeTimeFormatted = domainEvent.TeeTime.ToString("h:mm tt");
        var message = $"{courseName}: A {teeTimeFormatted} tee time just opened for today! Reply Y to claim or N to pass. You have {ResponseWindowMinutes} min to respond.";

        await textMessageService.SendAsync(firstEntry.GolferPhone, message, ct);

        logger.LogInformation(
            "Offer {OfferId} sent to golfer {GolferPhone} for tee time {TeeTime} (request {TeeTimeRequestId}).",
            offer.Id, firstEntry.GolferPhone, domainEvent.TeeTime, domainEvent.TeeTimeRequestId);
    }
}
