using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public interface ITeeSheetRepository : IRepository<TeeSheet>
{
    void Add(TeeSheet sheet);
    Task<TeeSheet?> GetByCourseAndDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default);
    Task<TeeSheet?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default);
}
