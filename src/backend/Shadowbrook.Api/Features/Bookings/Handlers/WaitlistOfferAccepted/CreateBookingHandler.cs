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
        IBookingRepository bookingRepository)
    {
        var opening = await openingRepository.GetByIdAsync(evt.OpeningId)
            ?? throw new InvalidOperationException($"TeeTimeOpening {evt.OpeningId} not found for event {nameof(WaitlistOfferAccepted)}.");

        var golfer = await golferRepository.GetByIdAsync(evt.GolferId)
            ?? throw new InvalidOperationException($"Golfer {evt.GolferId} not found for event {nameof(WaitlistOfferAccepted)}.");

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
