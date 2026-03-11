using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class WalkUpWaitlistRepository(ApplicationDbContext db) : IWalkUpWaitlistRepository
{
    public async Task<WalkUpWaitlist?> GetByIdAsync(Guid id)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.Entries)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.Entries)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date);
    }

    public async Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.Entries)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date
                && w.Status == WaitlistStatus.Open);
    }

    public void Add(WalkUpWaitlist waitlist) =>
        db.WalkUpWaitlists.Add(waitlist);

    public async Task SaveAsync() =>
        await db.SaveChangesAsync();
}
