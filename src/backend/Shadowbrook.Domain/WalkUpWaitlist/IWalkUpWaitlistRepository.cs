namespace Shadowbrook.Domain.WalkUpWaitlist;

public interface IWalkUpWaitlistRepository
{
    Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(WalkUpWaitlist waitlist);
    Task SaveAsync();
}
