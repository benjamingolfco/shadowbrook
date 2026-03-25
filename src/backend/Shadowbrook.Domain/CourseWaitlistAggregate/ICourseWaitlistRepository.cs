namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public interface ICourseWaitlistRepository
{
    Task<CourseWaitlist?> GetByIdAsync(Guid id);
    Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(CourseWaitlist waitlist);
}
