using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.Bookings;

public static class TeeTimeSlotFillFailedHandler
{
    public static async Task Handle(TeeTimeSlotFillFailed domainEvent, IWaitlistOfferRepository offerRepository)
    {
        var offer = await offerRepository.GetByIdAsync(domainEvent.OfferId);
        if (offer is null)
        {
            return;
        }

        offer.Reject(domainEvent.Reason);
    }
}
