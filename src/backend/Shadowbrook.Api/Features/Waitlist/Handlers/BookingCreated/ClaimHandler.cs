using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class BookingCreatedClaimHandler
{
    public static async Task<ConfirmBookingCommand?> Handle(
        BookingCreated evt,
        ITeeTimeOpeningRepository openingRepository,
        ITimeProvider timeProvider)
    {
        var opening = await openingRepository.GetActiveByCourseDateTimeAsync(
            evt.CourseId, evt.Date, evt.TeeTime);

        if (opening is null)
        {
            // No active opening — not a waitlist booking, confirm directly
            return new ConfirmBookingCommand(evt.BookingId);
        }

        // Waitlist booking — claim the slot. The TeeTimeOpeningClaimed/Rejected
        // event will be picked up by BookingConfirmationPolicy.
        opening.TryClaim(evt.BookingId, evt.GolferId, evt.GroupSize, timeProvider);
        return null;
    }
}
