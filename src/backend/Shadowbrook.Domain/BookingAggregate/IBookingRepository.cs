using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate;

public interface IBookingRepository : IRepository<Booking>
{
    void Add(Booking booking);
    Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, TeeTime teeTime, CancellationToken ct = default);
}
