using Teeforce.Domain.Common;

namespace Teeforce.Domain.BookingAggregate;

public interface IBookingRepository : IRepository<Booking>
{
    void Add(Booking booking);
    Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, BookingDateTime teeTime, CancellationToken ct = default);
    Task<List<Booking>> GetByTeeTimeIdsAsync(List<Guid> teeTimeIds, CancellationToken ct = default);
}
