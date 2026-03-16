using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class TeeTimeRequestAddedNotifyHandler(
    ApplicationDbContext db,
    IWaitlistOfferRepository repository,
    ITextMessageService textMessageService,
    IConfiguration configuration)
    : IDomainEventHandler<TeeTimeRequestAdded>
{
    public async Task HandleAsync(TeeTimeRequestAdded domainEvent, CancellationToken ct = default)
    {
        // Find the open waitlist for this course + date
        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.CourseId == domainEvent.CourseId && w.Date == domainEvent.Date, ct);

        if (waitlist is null)
        {
            return;
        }

        // Query eligible golfers by joining entries with golfer records
        // Client-side ordering to avoid SQLite DateTimeOffset limitation in tests
        var eligibleGolfers = (await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id
                && e.IsWalkUp == true
                && e.IsReady == true
                && e.RemovedAt == null)
            .Join(db.Golfers.IgnoreQueryFilters(),
                e => e.GolferId, g => g.Id,
                (e, g) => new { Entry = e, Golfer = g })
            .ToListAsync(ct))
            .OrderBy(eg => eg.Entry.JoinedAt)
            .ToList();

        if (eligibleGolfers.Count == 0)
        {
            return;
        }

        // Get course name for SMS messages
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        var offers = new List<(WaitlistOffer Offer, string Phone)>();

        foreach (var eg in eligibleGolfers)
        {
            var offer = WaitlistOffer.Create(
                teeTimeRequestId: domainEvent.TeeTimeRequestId,
                golferWaitlistEntryId: eg.Entry.Id);
            offers.Add((offer, eg.Golfer.Phone));
        }

        repository.AddRange(offers.Select(o => o.Offer));
        await repository.SaveAsync();

        // Send SMS to each eligible golfer
        var baseUrl = configuration["App:BaseUrl"] ?? "http://localhost:3000";

        foreach (var (offer, phone) in offers)
        {
            var message = $"{courseName}: {domainEvent.TeeTime:h:mm tt} tee time just opened! Claim your spot: {baseUrl}/book/walkup/{offer.Token} - You have 15 minutes.";
            await textMessageService.SendAsync(phone, message, ct);
        }
    }
}
