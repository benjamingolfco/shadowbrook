using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeSheetAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class TeeSheetRepository(ApplicationDbContext db) : ITeeSheetRepository
{
    public async Task<TeeSheet?> GetByIdAsync(Guid id) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<TeeSheet?> GetByCourseAndDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(s => s.CourseId == courseId && s.Date == date, ct);

    public async Task<TeeSheet?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(s => s.Intervals.Any(i => i.Id == intervalId), ct);

    public void Add(TeeSheet sheet) => db.TeeSheets.Add(sheet);
}
