namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public interface ITeeTimeRequestRepository
{
    Task<bool> ExistsAsync(Guid courseId, DateOnly date, TimeOnly teeTime);
    Task<TeeTimeRequest?> GetByIdAsync(Guid id);
    Task<List<TeeTimeRequest>> GetByCourseAndDateAsync(Guid courseId, DateOnly date);
    void Add(TeeTimeRequest request);
    Task SaveAsync();
}
