using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Repositories;

public class BookingRepository(ApplicationDbContext db) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id) =>
        await db.Bookings.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);

    public void Add(Booking booking) =>
        db.Bookings.Add(booking);

    public async Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, BookingDateTime teeTime, CancellationToken ct = default) =>
        await db.Bookings
            .Where(b => b.CourseId == courseId && b.TeeTime.Value == teeTime.Value)
            .ToListAsync(ct);
}
