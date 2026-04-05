using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.GolferAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCreatedConfirmationSmsHandler
{
    public static async Task Handle(
        BookingCreated domainEvent,
        IGolferRepository golferRepository,
        IBookingRepository bookingRepository,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        CancellationToken ct)
    {
        var golfer = await golferRepository.GetRequiredByIdAsync(domainEvent.GolferId);

        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Course {domainEvent.CourseId} not found for event {nameof(BookingCreated)}.");

        var booking = await bookingRepository.GetRequiredByIdAsync(domainEvent.BookingId);

        var message = $"You're booked! {courseName} at {booking.TeeTime.Time:h:mm tt} on {booking.TeeTime.Date:MMMM d, yyyy}. See you on the course!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
