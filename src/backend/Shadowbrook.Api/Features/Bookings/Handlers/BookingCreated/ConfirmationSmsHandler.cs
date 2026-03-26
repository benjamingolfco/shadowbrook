using Microsoft.EntityFrameworkCore;
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

        var message = $"You're booked! {courseName} at {booking.Time:h:mm tt} on {booking.Date:MMMM d, yyyy}. See you on the course!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
