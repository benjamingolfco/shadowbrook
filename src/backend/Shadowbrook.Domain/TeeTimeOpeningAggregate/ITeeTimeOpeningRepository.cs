using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public interface ITeeTimeOpeningRepository
{
    Task<TeeTimeOpening?> GetByIdAsync(Guid id);
    Task<TeeTimeOpening?> GetActiveByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime);
    Task<TeeTimeOpening?> GetByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime);
    void Add(TeeTimeOpening opening);
}
