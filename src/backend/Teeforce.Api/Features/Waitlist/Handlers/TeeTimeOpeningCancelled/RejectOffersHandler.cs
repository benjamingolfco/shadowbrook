using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class TeeTimeOpeningCancelledRejectOffersHandler
{
    public static async Task Handle(
        TeeTimeOpeningCancelled evt,
        IWaitlistOfferRepository offerRepository)
    {
        var pendingOffers = await offerRepository.GetPendingByOpeningAsync(evt.OpeningId);

        foreach (var offer in pendingOffers)
        {
            offer.Reject("Tee time opening has been cancelled by the course.");
        }
    }
}
