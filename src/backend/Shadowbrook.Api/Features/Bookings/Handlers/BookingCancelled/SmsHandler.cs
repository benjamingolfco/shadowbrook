using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class BookingCancelledSmsHandler
{
    public static async Task Handle(
        BookingCancelled evt,
        IBookingRepository bookingRepository,
        IGolferRepository golferRepository,
        ICourseRepository courseRepository,
        ITextMessageService textMessageService,
        ILogger logger,
        CancellationToken ct)
    {
        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        var golfer = await golferRepository.GetByIdAsync(booking.GolferId);

        if (golfer is null)
        {
            logger.LogWarning("Golfer {GolferId} not found for BookingCancelled event {EventId}, skipping SMS", booking.GolferId, evt.EventId);
            return;
        }

        var course = await courseRepository.GetByIdAsync(booking.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for BookingCancelled event {EventId}, skipping SMS", booking.CourseId, evt.EventId);
            return;
        }

        var message = $"Your tee time at {course.Name} on {booking.TeeTime.Date:MMMM d, yyyy} at {booking.TeeTime.Time:h:mm tt} has been cancelled.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
