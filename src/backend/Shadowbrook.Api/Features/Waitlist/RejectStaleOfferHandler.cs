using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist;

public static class RejectStaleOfferHandler
{
    public static async Task<WaitlistOfferStale?> Handle(
        RejectStaleOffer command,
        IWaitlistOfferRepository offerRepository)
    {
        var offer = await offerRepository.GetByIdAsync(command.WaitlistOfferId);
        if (offer is null || offer.Status != OfferStatus.Pending)
        {
            return null;
        }

        offer.Reject("Offer expired — no response received.");

        return new WaitlistOfferStale
        {
            WaitlistOfferId = offer.Id,
            OpeningId = command.OpeningId
        };
    }
}
