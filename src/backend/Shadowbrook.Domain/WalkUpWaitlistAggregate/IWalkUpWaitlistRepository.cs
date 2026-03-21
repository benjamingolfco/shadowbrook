namespace Shadowbrook.Domain.WalkUpWaitlistAggregate;

public interface IWalkUpWaitlistRepository
{
    Task<WalkUpWaitlist?> GetByIdAsync(Guid id);
    Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(WalkUpWaitlist waitlist);
}
