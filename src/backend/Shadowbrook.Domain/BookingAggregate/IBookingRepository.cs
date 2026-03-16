namespace Shadowbrook.Domain.BookingAggregate;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id);
    void Add(Booking booking);
    Task SaveAsync();
}
