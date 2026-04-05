using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseWaitlistAggregate;

public interface ICourseWaitlistRepository : IRepository<CourseWaitlist>
{
    Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(CourseWaitlist waitlist);
}
