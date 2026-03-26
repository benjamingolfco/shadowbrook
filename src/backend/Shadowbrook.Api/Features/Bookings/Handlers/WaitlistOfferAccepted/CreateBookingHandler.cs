using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class WaitlistOfferAcceptedCreateBookingHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        ITeeTimeOpeningRepository openingRepository,
        IGolferRepository golferRepository,
        IGolferWaitlistEntryRepository entryRepository,
        IBookingRepository bookingRepository,
        ILogger logger)
    {
        var opening = await openingRepository.GetByIdAsync(evt.OpeningId);
        if (opening is null)
        {
            logger.LogWarning("TeeTimeOpening {OpeningId} not found, skipping booking creation for offer {OfferId}", evt.OpeningId, evt.WaitlistOfferId);
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(evt.GolferId);
        if (golfer is null)
        {
            logger.LogWarning("Golfer {GolferId} not found, skipping booking creation for offer {OfferId}", evt.GolferId, evt.WaitlistOfferId);
            return;
        }

        var booking = Booking.Create(
            bookingId: Guid.CreateVersion7(),
            courseId: opening.CourseId,
            golferId: evt.GolferId,
            date: opening.Date,
            time: opening.TeeTime,
            golferName: golfer.FullName,
            playerCount: evt.GroupSize,
            openingId: opening.Id);

        bookingRepository.Add(booking);
    }
}
