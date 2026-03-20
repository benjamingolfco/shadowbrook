using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public static class TeeTimeRequestFulfilledHandler
{
    public static async Task Handle(TeeTimeRequestFulfilled domainEvent, IWaitlistOfferRepository offerRepository)
    {
        var pendingOffers = await offerRepository.GetPendingByRequestAsync(domainEvent.TeeTimeRequestId);

        foreach (var offer in pendingOffers)
        {
            offer.Reject("Tee time has been filled.");
        }

    }
}
