using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public static class NotifyNextEligibleGolferHandler
{
    public static async Task Handle(
        NotifyNextEligibleGolfer command,
        ITeeTimeRequestRepository requestRepository,
        IWaitlistOfferRepository offerRepository,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var request = await requestRepository.GetByIdAsync(command.TeeTimeRequestId);
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
        var nextEntry = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id
                && e.IsWalkUp == true
                && e.RemovedAt == null
                && !alreadyOfferedEntryIds.Contains(e.Id)
                && e.GroupSize <= request.RemainingSlots)
            .Join(db.Golfers.IgnoreQueryFilters(),
                e => e.GolferId, g => g.Id,
                (e, g) => new { Entry = e, Golfer = g })
            .OrderBy(eg => eg.Entry.JoinedAt)
            .ThenBy(eg => eg.Entry.Id)
            .FirstOrDefaultAsync(ct);

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

        var baseUrl = configuration["App:FrontendUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException("App:FrontendUrl is not configured. SMS offer links require a valid frontend URL.");
        }
        var message = $"{courseName}: {request.TeeTime:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
        await textMessageService.SendAsync(nextEntry.Golfer.Phone, message, ct);

        offer.MarkNotified();
    }
}
