using Microsoft.Extensions.Logging;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingCancelledNotification(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingCancelledNotificationSmsFormatter : SmsFormatter<BookingCancelledNotification>
{
    protected override string FormatMessage(BookingCancelledNotification n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d, yyyy} at {n.Time:h:mm tt} has been cancelled.";
}

public static class BookingCancelledSmsHandler
{
    public static async Task Handle(
        BookingCancelled evt,
        IBookingRepository bookingRepository,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        ILogger logger,
        CancellationToken ct)
    {
        if (evt.PreviousStatus != BookingStatus.Confirmed)
        {
            logger.LogWarning("Booking {BookingId} was cancelled from {PreviousStatus} status, skipping SMS (only confirmed bookings receive cancellation SMS)", evt.BookingId, evt.PreviousStatus);
            return;
        }

        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        var course = await courseRepository.GetByIdAsync(booking.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for BookingCancelled event {EventId}, skipping SMS", booking.CourseId, evt.EventId);
            return;
        }

        await notificationService.Send(booking.GolferId, new BookingCancelledNotification(course.Name, booking.TeeTime.Date, booking.TeeTime.Time), ct);
    }
}
