using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class TeeTimeOpeningRepository(ApplicationDbContext db) : ITeeTimeOpeningRepository
{
    public async Task<TeeTimeOpening?> GetByIdAsync(Guid id) =>
        await db.TeeTimeOpenings.FindAsync(id);

    public async Task<TeeTimeOpening?> GetActiveByCourseDateTimeAsync(Guid courseId, DateOnly date, TimeOnly teeTime)
    {
        return await db.TeeTimeOpenings
            .FirstOrDefaultAsync(o => o.CourseId == courseId && o.TeeTime.Date == date
                && o.TeeTime.Time == teeTime && o.Status == TeeTimeOpeningStatus.Open);
    }

    public async Task<TeeTimeOpening?> GetByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime)
    {
        return await db.TeeTimeOpenings
            .FirstOrDefaultAsync(o => o.CourseId == courseId && o.TeeTime.Date == teeTime.Date
                && o.TeeTime.Time == teeTime.Time);
    }

    public void Add(TeeTimeOpening opening) =>
        db.TeeTimeOpenings.Add(opening);
}
