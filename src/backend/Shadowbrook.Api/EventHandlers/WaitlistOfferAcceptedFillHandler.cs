using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public static class WaitlistOfferAcceptedFillHandler
{
    public static async Task<object?> Handle(
        WaitlistOfferAccepted domainEvent,
        ITeeTimeRequestRepository requestRepository,
        IGolferWaitlistEntryRepository entryRepository)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return null;
        }

        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            return new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = "Tee time request not found."
            };
        }

        var result = request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId);

        if (!result.Success)
        {
            return new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = result.RejectionReason ?? "Unable to fill slot."
            };
        }

        return new TeeTimeSlotFilled
        {
            TeeTimeRequestId = domainEvent.TeeTimeRequestId,
            BookingId = domainEvent.BookingId,
            GolferId = domainEvent.GolferId
        };
    }
}
