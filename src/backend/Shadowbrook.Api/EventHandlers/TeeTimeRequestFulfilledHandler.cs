using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class TeeTimeRequestFulfilledHandler(
    IWaitlistOfferRepository offerRepository)
    : IDomainEventHandler<TeeTimeRequestFulfilled>
{
    public async Task HandleAsync(TeeTimeRequestFulfilled domainEvent, CancellationToken ct = default)
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
