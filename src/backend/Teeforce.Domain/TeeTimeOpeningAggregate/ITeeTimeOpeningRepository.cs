using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOpeningAggregate;

public interface ITeeTimeOpeningRepository : IRepository<TeeTimeOpening>
{
    Task<TeeTimeOpening?> GetActiveByCourseTeeTimeAsync(Guid courseId, BookingDateTime teeTime);
    Task<TeeTimeOpening?> GetByCourseTeeTimeAsync(Guid courseId, BookingDateTime teeTime);
    Task<List<TeeTimeOpening>> FindActiveOpeningsForCourseDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default);
    void Add(TeeTimeOpening opening);
}
