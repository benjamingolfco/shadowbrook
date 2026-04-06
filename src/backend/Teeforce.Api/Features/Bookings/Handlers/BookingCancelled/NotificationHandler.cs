using Microsoft.Extensions.Logging;
using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCancelledNotificationHandler
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
            logger.LogWarning("Booking {BookingId} was cancelled from {PreviousStatus} status, skipping notification (only confirmed bookings receive cancellation notification)", evt.BookingId, evt.PreviousStatus);
            return;
        }

        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        var course = await courseRepository.GetByIdAsync(booking.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for BookingCancelled event {EventId}, skipping notification", booking.CourseId, evt.EventId);
            return;
        }

        await notificationService.Send(booking.GolferId, new BookingCancellation(course.Name, booking.TeeTime.Date, booking.TeeTime.Time), ct);
    }
}
