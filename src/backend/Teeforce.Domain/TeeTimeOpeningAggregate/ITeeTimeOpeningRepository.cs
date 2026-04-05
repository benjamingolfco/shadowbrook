using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOpeningAggregate;

public interface ITeeTimeOpeningRepository : IRepository<TeeTimeOpening>
{
    Task<TeeTimeOpening?> GetActiveByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime);
    Task<TeeTimeOpening?> GetByCourseTeeTimeAsync(Guid courseId, TeeTime teeTime);
    Task<List<TeeTimeOpening>> FindActiveOpeningsForCourseDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default);
    void Add(TeeTimeOpening opening);
}
