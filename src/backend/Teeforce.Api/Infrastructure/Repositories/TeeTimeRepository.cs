using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class TeeTimeRepository(ApplicationDbContext db) : ITeeTimeRepository
{
    public async Task<TeeTime?> GetByIdAsync(Guid id) =>
        await db.TeeTimes
            .Include(t => t.Claims)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<TeeTime?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default) =>
        await db.TeeTimes
            .Include(t => t.Claims)
            .FirstOrDefaultAsync(t => t.TeeSheetIntervalId == intervalId, ct);

    public async Task<List<TeeTime>> GetByTeeSheetIdAsync(Guid teeSheetId, CancellationToken ct = default) =>
        await db.TeeTimes
            .Include(t => t.Claims)
            .Where(t => t.TeeSheetId == teeSheetId)
            .ToListAsync(ct);

    public void Add(TeeTime teeTime) => db.TeeTimes.Add(teeTime);


    public void Remove(TeeTime teeTime) => db.TeeTimes.Remove(teeTime);
}
