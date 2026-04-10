using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate;

public interface ITeeTimeRepository : IRepository<TeeTime>
{
    void Add(TeeTime teeTime);
    Task<TeeTime?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default);
    Task<List<TeeTime>> GetByTeeSheetIdAsync(Guid teeSheetId, CancellationToken ct = default);
}
