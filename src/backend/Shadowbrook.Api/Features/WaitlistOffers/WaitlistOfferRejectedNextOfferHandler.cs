using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public static class WaitlistOfferRejectedNextOfferHandler
{
    public static async Task Handle(
        WaitlistOfferRejected domainEvent,
        IWaitlistOfferRepository offerRepository,
        ITeeTimeRequestRepository requestRepository,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        IConfiguration configuration,
        CancellationToken ct)
    {
        // Get the rejected offer to find the TeeTimeRequestId
        var rejectedOffer = await offerRepository.GetByIdAsync(domainEvent.WaitlistOfferId);
        if (rejectedOffer is null)
        {
            return;
        }

        var request = await requestRepository.GetByIdAsync(rejectedOffer.TeeTimeRequestId);
        if (request is null || request.Status != TeeTimeRequestStatus.Pending)
        {
            return;
        }

        if (request.RemainingSlots <= 0)
        {
            return;
        }

        // Find the waitlist for this course + date
        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.CourseId == request.CourseId && w.Date == request.Date, ct);

        if (waitlist is null)
        {
            return;
        }

        // Find GolferWaitlistEntryIds that already have an offer for this request
        var alreadyOfferedEntryIds = await db.WaitlistOffers
            .Where(o => o.TeeTimeRequestId == request.Id)
            .Select(o => o.GolferWaitlistEntryId)
            .ToListAsync(ct);

        // Find the next eligible golfer — active, walk-up, ready, not already offered, group fits
        // Client-side ordering to avoid SQLite DateTimeOffset limitation in tests
        var nextEntry = (await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id
                && e.IsWalkUp == true
                && e.IsReady == true
                && e.RemovedAt == null
                && !alreadyOfferedEntryIds.Contains(e.Id)
                && e.GroupSize <= request.RemainingSlots)
            .Join(db.Golfers.IgnoreQueryFilters(),
                e => e.GolferId, g => g.Id,
                (e, g) => new { Entry = e, Golfer = g })
            .ToListAsync(ct))
            .OrderBy(eg => eg.Entry.JoinedAt)
            .ThenBy(eg => eg.Entry.Id.ToString())
            .FirstOrDefault();

        if (nextEntry is null)
        {
            return;
        }

        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == request.CourseId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        var offer = WaitlistOffer.Create(
            teeTimeRequestId: request.Id,
            golferWaitlistEntryId: nextEntry.Entry.Id);

        offerRepository.Add(offer);

        var baseUrl = configuration["App:BaseUrl"] ?? "http://localhost:3000";
        var message = $"{courseName}: {request.TeeTime:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
        await textMessageService.SendAsync(nextEntry.Golfer.Phone, message, ct);
    }
}
