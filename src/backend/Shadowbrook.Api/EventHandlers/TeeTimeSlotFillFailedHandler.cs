using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class TeeTimeSlotFillFailedHandler(
    IWaitlistOfferRepository offerRepository)
    : IDomainEventHandler<TeeTimeSlotFillFailed>
{
    public async Task HandleAsync(TeeTimeSlotFillFailed domainEvent, CancellationToken ct = default)
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
