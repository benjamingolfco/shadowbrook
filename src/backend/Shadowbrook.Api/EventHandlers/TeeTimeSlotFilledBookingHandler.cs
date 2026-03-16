using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class TeeTimeSlotFilledBookingHandler(
    ITeeTimeRequestRepository requestRepository,
    IGolferRepository golferRepository,
    IWaitlistOfferRepository offerRepository,
    IGolferWaitlistEntryRepository entryRepository,
    IBookingRepository bookingRepository)
    : IDomainEventHandler<TeeTimeSlotFilled>
{
    public async Task HandleAsync(TeeTimeSlotFilled domainEvent, CancellationToken ct = default)
    {
        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(domainEvent.GolferId);
        if (golfer is null)
        {
            return;
        }

        var offer = await offerRepository.GetByBookingIdAsync(domainEvent.BookingId);
        if (offer is null)
        {
            return;
        }

        var entry = await entryRepository.GetByIdAsync(offer.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var booking = Booking.Create(
            bookingId: domainEvent.BookingId,
            courseId: request.CourseId,
            golferId: domainEvent.GolferId,
            date: request.Date,
            time: request.TeeTime,
            golferName: golfer.FullName,
            playerCount: entry.GroupSize);

        bookingRepository.Add(booking);
        await bookingRepository.SaveAsync();
    }
}
