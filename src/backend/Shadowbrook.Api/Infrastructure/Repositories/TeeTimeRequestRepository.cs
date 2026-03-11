using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.TeeTimeRequestAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class TeeTimeRequestRepository(ApplicationDbContext db) : ITeeTimeRequestRepository
{
    public async Task<bool> ExistsAsync(Guid courseId, DateOnly date, TimeOnly teeTime)
    {
        return await db.TeeTimeRequests
            .AnyAsync(r => r.CourseId == courseId
                && r.Date == date
                && r.TeeTime == teeTime
                && r.Status == TeeTimeRequestStatus.Pending);
    }

    public async Task<List<TeeTimeRequest>> GetByCourseAndDateAsync(Guid courseId, DateOnly date)
    {
        return await db.TeeTimeRequests
            .Where(r => r.CourseId == courseId && r.Date == date)
            .ToListAsync();
    }

    public void Add(TeeTimeRequest request) =>
        db.TeeTimeRequests.Add(request);

    public async Task SaveAsync() =>
        await db.SaveChangesAsync();
}
