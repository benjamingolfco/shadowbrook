using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.TeeTimeOpenings;

public static class TeeTimeOpeningFilledRejectOffersHandler
{
    public static async Task Handle(
        TeeTimeOpeningFilled evt,
        IWaitlistOfferRepository offerRepository)
    {
        var pendingOffers = await offerRepository.GetPendingByOpeningAsync(evt.OpeningId);

        foreach (var offer in pendingOffers)
        {
            offer.Reject("Tee time has been filled.");
        }
    }
}
