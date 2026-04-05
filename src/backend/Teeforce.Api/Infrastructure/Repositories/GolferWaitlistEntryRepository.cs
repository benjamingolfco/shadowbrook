using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

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
        Guid courseId, DateOnly date, TimeOnly teeTime, int maxGroupSize, CancellationToken ct = default)
    {
        var teeTimeDateTime = date.ToDateTime(teeTime);

        return await db.GolferWaitlistEntries
            .Where(e => e.RemovedAt == null)
            .Where(e => e.WindowStart <= teeTimeDateTime && e.WindowEnd >= teeTimeDateTime)
            .Where(e => e.GroupSize <= maxGroupSize)
            .Where(e => db.CourseWaitlists
                .Any(w => w.Id == e.CourseWaitlistId && w.CourseId == courseId && w.Date == date))
            .Where(e => !db.WaitlistOffers
                .Any(o => o.GolferWaitlistEntryId == e.Id && o.Status == OfferStatus.Pending))
            .OrderBy(e => e.JoinedAt)
            .ToListAsync(ct);
    }
}
