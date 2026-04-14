using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public interface ITeeSheetRepository : IRepository<TeeSheet>
{
    void Add(TeeSheet sheet);
    Task<TeeSheet?> GetByCourseAndDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default);
    Task<List<TeeSheet>> GetByCourseAndDatesAsync(Guid courseId, List<DateOnly> dates, CancellationToken ct = default);
    Task<TeeSheet?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default);
    Task<List<TeeSheet>> GetFutureByCourseAsync(Guid courseId, DateOnly fromDate, CancellationToken ct = default);
}
