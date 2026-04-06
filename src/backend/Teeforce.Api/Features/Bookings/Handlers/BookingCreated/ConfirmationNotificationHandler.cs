using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCreatedConfirmationNotificationHandler
{
    public static async Task Handle(
        BookingCreated domainEvent,
        IBookingRepository bookingRepository,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        CancellationToken ct)
    {
        var course = await courseRepository.GetRequiredByIdAsync(domainEvent.CourseId);
        var booking = await bookingRepository.GetRequiredByIdAsync(domainEvent.BookingId);

        await notificationService.Send(domainEvent.GolferId, new BookingConfirmation(course.Name, booking.TeeTime.Date, booking.TeeTime.Time), ct);
    }
}
