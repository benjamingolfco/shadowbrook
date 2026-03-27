using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public interface ITeeTimeOpeningRepository
{
    Task<TeeTimeOpening?> GetByIdAsync(Guid id);
    Task<TeeTimeOpening?> GetActiveByCourseDateTimeAsync(Guid courseId, DateOnly date, TimeOnly teeTime);
    Task<TeeTimeOpening?> GetByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime);
    void Add(TeeTimeOpening opening);
}
