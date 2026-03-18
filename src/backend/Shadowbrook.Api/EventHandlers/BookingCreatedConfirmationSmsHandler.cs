using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.EventHandlers;

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
        var golfer = await golferRepository.GetByIdAsync(domainEvent.GolferId);
        if (golfer is null)
        {
            return;
        }

        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);

        if (courseName is null)
        {
            return;
        }

        var booking = await bookingRepository.GetByIdAsync(domainEvent.BookingId);
        if (booking is null)
        {
            return;
        }

        var message = $"You're booked! {courseName} at {booking.Time:h:mm tt} on {booking.Date:MMMM d, yyyy}. See you on the course!";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
