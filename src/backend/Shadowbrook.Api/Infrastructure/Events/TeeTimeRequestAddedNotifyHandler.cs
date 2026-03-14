using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Events;

namespace Shadowbrook.Api.Infrastructure.Events;

public class TeeTimeRequestAddedNotifyHandler(
    ApplicationDbContext db,
    ITextMessageService textMessageService,
    IConfiguration configuration)
    : IDomainEventHandler<TeeTimeRequestAdded>
{
    public async Task HandleAsync(TeeTimeRequestAdded domainEvent, CancellationToken ct = default)
    {
        // Query eligible golfers on the waitlist
        // Note: Order by JoinedAt client-side to avoid SQLite limitation with DateTimeOffset in ORDER BY
        var eligibleGolfers = (await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == domainEvent.WaitlistId
                && e.IsWalkUp == true
                && e.IsReady == true
                && e.RemovedAt == null)
            .ToListAsync(ct))
            .OrderBy(e => e.JoinedAt)
            .ToList();

        if (eligibleGolfers.Count == 0)
        {
            return;
        }

        // Get course name for the offer
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var offers = new List<WaitlistOffer>();

        foreach (var golferEntry in eligibleGolfers)
        {
            var offer = new WaitlistOffer
            {
                Id = Guid.CreateVersion7(),
                Token = Guid.CreateVersion7(),
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                GolferWaitlistEntryId = golferEntry.Id,
                CourseId = domainEvent.CourseId,
                CourseName = courseName,
                Date = domainEvent.Date,
                TeeTime = domainEvent.TeeTime,
                GolfersNeeded = domainEvent.GolfersNeeded,
                GolferName = golferEntry.GolferName,
                GolferPhone = golferEntry.GolferPhone,
                Status = OfferStatus.Pending,
                ExpiresAt = expiresAt,
                CreatedAt = now
            };
            offers.Add(offer);
        }

        db.WaitlistOffers.AddRange(offers);
        await db.SaveChangesAsync(ct);

        // Send SMS to each eligible golfer
        var baseUrl = configuration["App:BaseUrl"] ?? "http://localhost:3000";

        foreach (var offer in offers)
        {
            var message = $"{offer.CourseName}: {offer.TeeTime:h:mm tt} tee time just opened! Claim your spot: {baseUrl}/book/walkup/{offer.Token} - You have 15 minutes.";
            await textMessageService.SendAsync(offer.GolferPhone, message, ct);
        }
    }
}
