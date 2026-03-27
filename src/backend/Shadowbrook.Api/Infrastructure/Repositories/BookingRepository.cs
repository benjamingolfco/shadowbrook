using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class BookingRepository(ApplicationDbContext db) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id) =>
        await db.Bookings.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);

    public void Add(Booking booking) =>
        db.Bookings.Add(booking);

    public async Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, DateOnly date, TimeOnly teeTime, CancellationToken ct = default) =>
        await db.Bookings
            .Where(b => b.CourseId == courseId && b.TeeTime.Date == date && b.TeeTime.Time == teeTime)
            .ToListAsync(ct);
}
