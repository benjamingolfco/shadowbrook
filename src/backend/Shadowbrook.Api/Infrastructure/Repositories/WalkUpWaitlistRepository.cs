using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class WalkUpWaitlistRepository(ApplicationDbContext db) : IWalkUpWaitlistRepository
{
    public async Task<WalkUpWaitlistEntity?> GetByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.TeeTimeRequests)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date);
    }

    public async Task<WalkUpWaitlistEntity?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.TeeTimeRequests)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date
                && w.Status == WaitlistStatus.Open);
    }

    public void Add(WalkUpWaitlistEntity waitlist) =>
        db.WalkUpWaitlists.Add(waitlist);

    public async Task SaveAsync() =>
        await db.SaveChangesAsync();
}
