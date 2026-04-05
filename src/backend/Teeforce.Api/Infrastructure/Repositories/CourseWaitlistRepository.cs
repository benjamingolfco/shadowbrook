using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.CourseWaitlistAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class CourseWaitlistRepository(ApplicationDbContext db) : ICourseWaitlistRepository
{
    public async Task<CourseWaitlist?> GetByIdAsync(Guid id) =>
        await db.CourseWaitlists.FindAsync(id);

    public async Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.CourseWaitlists
            .OfType<WalkUpWaitlist>()
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date);
    }

    public async Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.CourseWaitlists
            .OfType<WalkUpWaitlist>()
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date
                && w.Status == WaitlistStatus.Open);
    }

    public void Add(CourseWaitlist waitlist) =>
        db.CourseWaitlists.Add(waitlist);
}
