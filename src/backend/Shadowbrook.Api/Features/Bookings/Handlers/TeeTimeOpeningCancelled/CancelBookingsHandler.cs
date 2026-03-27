using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public class TeeTimeOpeningCancelledCancelBookingsHandler(IBookingRepository bookingRepository, ILogger<TeeTimeOpeningCancelledCancelBookingsHandler> logger)
{
    private static readonly BookingStatus[] TerminalStatuses = [BookingStatus.Rejected, BookingStatus.Cancelled];

    public async Task Handle(TeeTimeOpeningCancelled domainEvent, CancellationToken ct)
    {
        var bookings = await bookingRepository.GetByCourseAndTeeTimeAsync(
            domainEvent.CourseId,
            new TeeTime(domainEvent.Date, domainEvent.TeeTime),
            ct);

        if (bookings.Count == 0)
        {
            logger.LogInformation(
                "No bookings found for cancelled opening {OpeningId} (Course: {CourseId}, Date: {Date}, TeeTime: {TeeTime})",
                domainEvent.OpeningId,
                domainEvent.CourseId,
                domainEvent.Date,
                domainEvent.TeeTime);
            return;
        }

        var activeBookings = bookings.Where(b => !TerminalStatuses.Contains(b.Status)).ToList();

        if (activeBookings.Count == 0)
        {
            logger.LogInformation(
                "No active bookings to cancel for cancelled opening {OpeningId} (found {TotalCount} bookings, all terminal)",
                domainEvent.OpeningId,
                bookings.Count);
            return;
        }

        foreach (var booking in activeBookings)
        {
            booking.Cancel();
        }

        logger.LogInformation(
            "Cancelled {CancelledCount} booking(s) for cancelled opening {OpeningId}",
            activeBookings.Count,
            domainEvent.OpeningId);
    }
}
