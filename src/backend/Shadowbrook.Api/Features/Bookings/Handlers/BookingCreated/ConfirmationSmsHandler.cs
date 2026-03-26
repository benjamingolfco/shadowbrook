using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class BookingCreatedConfirmationSmsHandler
{
    public static async Task Handle(
        BookingCreated domainEvent,
        IGolferRepository golferRepository,
        IBookingRepository bookingRepository,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        ILogger logger,
        CancellationToken ct)
    {
        var golfer = await golferRepository.GetByIdAsync(domainEvent.GolferId);
        if (golfer is null)
        {
            logger.LogWarning("Golfer {GolferId} not found, skipping booking confirmation SMS for booking {BookingId}", domainEvent.GolferId, domainEvent.BookingId);
            return;
        }

        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);

        if (courseName is null)
        {
            logger.LogWarning("Course {CourseId} not found, skipping booking confirmation SMS for booking {BookingId}", domainEvent.CourseId, domainEvent.BookingId);
            return;
        }

        var booking = await bookingRepository.GetByIdAsync(domainEvent.BookingId);
        if (booking is null)
        {
            logger.LogWarning("Booking {BookingId} not found, skipping confirmation SMS", domainEvent.BookingId);
            return;
        }

        var message = $"You're booked! {courseName} at {booking.Time:h:mm tt} on {booking.Date:MMMM d, yyyy}. See you on the course!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
