using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public static class WaitlistOfferAcceptedFillHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted domainEvent,
        ITeeTimeRequestRepository requestRepository,
        IGolferWaitlistEntryRepository entryRepository)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId)
            ?? throw new InvalidOperationException(
                $"GolferWaitlistEntry {domainEvent.GolferWaitlistEntryId} not found for accepted offer {domainEvent.WaitlistOfferId}.");

        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId)
            ?? throw new InvalidOperationException(
                $"TeeTimeRequest {domainEvent.TeeTimeRequestId} not found for accepted offer {domainEvent.WaitlistOfferId}.");

        request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId, domainEvent.WaitlistOfferId);
    }
}
