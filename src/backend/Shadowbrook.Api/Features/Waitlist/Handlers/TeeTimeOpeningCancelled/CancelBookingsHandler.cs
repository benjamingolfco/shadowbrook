using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public class TeeTimeOpeningCancelledCancelBookingsHandler(IBookingRepository bookingRepository, ILogger<TeeTimeOpeningCancelledCancelBookingsHandler> logger)
{
    public async Task Handle(TeeTimeOpeningCancelled domainEvent, CancellationToken ct)
    {
        var bookings = await bookingRepository.GetByCourseAndTeeTimeAsync(
            domainEvent.CourseId,
            domainEvent.Date,
            domainEvent.TeeTime,
            ct);

        if (bookings.Count == 0)
        {
            logger.LogWarning(
                "No bookings found for cancelled opening {OpeningId} (Course: {CourseId}, Date: {Date}, TeeTime: {TeeTime})",
                domainEvent.OpeningId,
                domainEvent.CourseId,
                domainEvent.Date,
                domainEvent.TeeTime);
            return;
        }

        var pendingBookings = bookings.Where(b => b.Status == BookingStatus.Pending).ToList();

        if (pendingBookings.Count == 0)
        {
            logger.LogInformation(
                "No pending bookings to reject for cancelled opening {OpeningId} (found {TotalCount} bookings, none pending)",
                domainEvent.OpeningId,
                bookings.Count);
            return;
        }

        foreach (var booking in pendingBookings)
        {
            booking.RejectBooking();
        }

        logger.LogInformation(
            "Rejected {RejectedCount} pending booking(s) for cancelled opening {OpeningId}",
            pendingBookings.Count,
            domainEvent.OpeningId);
    }
}
