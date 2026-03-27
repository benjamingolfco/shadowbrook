namespace Shadowbrook.Domain.BookingAggregate;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id);
    void Add(Booking booking);
    Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, DateOnly date, TimeOnly teeTime, CancellationToken ct = default);
}
