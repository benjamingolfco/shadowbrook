using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class TeeTimeRequestFulfilledHandler(
    IWaitlistOfferRepository offerRepository)
{
    public async Task Handle(TeeTimeRequestFulfilled domainEvent, CancellationToken ct)
    {
        var pendingOffers = await offerRepository.GetPendingByRequestAsync(domainEvent.TeeTimeRequestId);

        foreach (var offer in pendingOffers)
        {
            offer.Reject("Tee time has been filled.");
        }

        if (pendingOffers.Count > 0)
        {
            await offerRepository.SaveAsync();
        }
    }
}
