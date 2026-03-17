using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class TeeTimeSlotFillFailedHandler(
    IWaitlistOfferRepository offerRepository)
{
    public async Task Handle(TeeTimeSlotFillFailed domainEvent, CancellationToken ct)
    {
        var offer = await offerRepository.GetByIdAsync(domainEvent.OfferId);
        if (offer is null)
        {
            return;
        }

        offer.Reject(domainEvent.Reason);
        await offerRepository.SaveAsync();
    }
}
