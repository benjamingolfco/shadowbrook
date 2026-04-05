using Teeforce.Domain.TeeTimeOpeningAggregate.Events;
using Teeforce.Domain.WaitlistOfferAggregate;

namespace Teeforce.Api.Features.Waitlist.Handlers;

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
