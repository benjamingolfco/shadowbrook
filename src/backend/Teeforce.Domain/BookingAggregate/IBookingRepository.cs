using Teeforce.Domain.Common;

namespace Teeforce.Domain.BookingAggregate;

public interface IBookingRepository : IRepository<Booking>
{
    void Add(Booking booking);
    Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, TeeTime teeTime, CancellationToken ct = default);
}
