using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

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
}
