using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class TeeTimeOpeningRepository(ApplicationDbContext db) : ITeeTimeOpeningRepository
{
    public async Task<TeeTimeOpening?> GetByIdAsync(Guid id) =>
        await db.TeeTimeOpenings
            .Include(o => o.ClaimedSlots)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<TeeTimeOpening?> GetActiveByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime)
    {
        return await db.TeeTimeOpenings
            .Include(o => o.ClaimedSlots)
            .FirstOrDefaultAsync(o => o.CourseId == courseId
                && o.TeeTime.Value == teeTime.Value
                && o.Status == TeeTimeOpeningStatus.Open);
    }

    public async Task<TeeTimeOpening?> GetByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime)
    {
        return await db.TeeTimeOpenings
            .Include(o => o.ClaimedSlots)
            .FirstOrDefaultAsync(o => o.CourseId == courseId
                && o.TeeTime.Value == teeTime.Value);
    }

    public void Add(TeeTimeOpening opening) =>
        db.TeeTimeOpenings.Add(opening);
}
