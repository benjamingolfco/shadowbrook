using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingConfirmedNotification(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingConfirmedNotificationSmsFormatter : SmsFormatter<BookingConfirmedNotification>
{
    protected override string FormatMessage(BookingConfirmedNotification n) =>
        $"You're booked! {n.CourseName} at {n.Time:h:mm tt} on {n.Date:MMMM d, yyyy}. See you on the course!";
}

public static class BookingCreatedConfirmationSmsHandler
{
    public static async Task Handle(
        BookingCreated domainEvent,
        IBookingRepository bookingRepository,
        ApplicationDbContext db,
        INotificationService notificationService,
        CancellationToken ct)
    {
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Course {domainEvent.CourseId} not found for event {nameof(BookingCreated)}.");

        var booking = await bookingRepository.GetRequiredByIdAsync(domainEvent.BookingId);

        await notificationService.Send(domainEvent.GolferId, new BookingConfirmedNotification(courseName, booking.TeeTime.Date, booking.TeeTime.Time), ct);
    }
}
