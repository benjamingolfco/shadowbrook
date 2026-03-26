using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class GolferWaitlistEntryRepository(ApplicationDbContext db) : IGolferWaitlistEntryRepository
{
    public async Task<GolferWaitlistEntry?> GetByIdAsync(Guid id) =>
        await db.GolferWaitlistEntries.FindAsync(id);

    public async Task<GolferWaitlistEntry?> GetActiveByWaitlistAndGolferAsync(Guid courseWaitlistId, Guid golferId)
    {
        return await db.GolferWaitlistEntries
            .FirstOrDefaultAsync(e => e.CourseWaitlistId == courseWaitlistId
                && e.GolferId == golferId
                && e.RemovedAt == null);
    }

    public async Task<List<GolferWaitlistEntry>> GetActiveByWaitlistAsync(Guid courseWaitlistId)
    {
        return await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == courseWaitlistId && e.RemovedAt == null)
            .OrderBy(e => e.JoinedAt)
            .ToListAsync();
    }

    public void Add(GolferWaitlistEntry entry) =>
        db.GolferWaitlistEntries.Add(entry);

    public async Task<List<GolferWaitlistEntry>> FindEligibleEntriesAsync(
        Guid courseId, DateOnly date, TimeOnly teeTime, int maxGroupSize, Guid openingId, CancellationToken ct = default)
    {
        return await db.GolferWaitlistEntries
            .Where(e => e.RemovedAt == null)
            .Where(e => e.WindowStart <= teeTime && e.WindowEnd >= teeTime)
            .Where(e => e.GroupSize <= maxGroupSize)
            .Where(e => db.CourseWaitlists
                .Any(w => w.Id == e.CourseWaitlistId && w.CourseId == courseId && w.Date == date))
            .Where(e => !db.WaitlistOffers
                .Any(o => o.GolferWaitlistEntryId == e.Id
                    && o.OpeningId == openingId
                    && (o.Status == OfferStatus.Pending || o.Status == OfferStatus.Accepted)))
            .OrderBy(e => e.JoinedAt)
            .ToListAsync(ct);
    }
}
