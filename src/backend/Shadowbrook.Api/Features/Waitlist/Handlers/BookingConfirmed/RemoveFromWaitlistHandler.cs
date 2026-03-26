using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class BookingConfirmedRemoveFromWaitlistHandler
{
    public static async Task Handle(
        BookingConfirmed evt,
        IBookingRepository bookingRepository,
        ICourseWaitlistRepository waitlistRepository,
        IGolferWaitlistEntryRepository entryRepository,
        ITimeProvider timeProvider,
        ILogger logger)
    {
        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        // Look up the course waitlist for this booking's date
        var waitlist = await waitlistRepository.GetByCourseDateAsync(
            booking.CourseId,
            booking.TeeTime.Date);

        if (waitlist is null)
        {
            logger.LogWarning(
                "No waitlist found for course {CourseId} on {Date}, cannot remove golfer {GolferId}",
                booking.CourseId,
                booking.TeeTime.Date,
                evt.GolferId);
            return;
        }

        // Find the golfer's active entry on this waitlist
        var entry = await entryRepository.GetActiveByWaitlistAndGolferAsync(waitlist.Id, evt.GolferId);
        if (entry is null)
        {
            logger.LogWarning(
                "No active waitlist entry found for golfer {GolferId} on waitlist {WaitlistId}",
                evt.GolferId,
                waitlist.Id);
            return;
        }

        entry.Remove(timeProvider);
    }
}
