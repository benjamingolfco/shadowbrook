using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class RejectStaleOfferHandler
{
    public static async Task<WaitlistOfferStale?> Handle(
        RejectStaleOffer command,
        IWaitlistOfferRepository offerRepository,
        ILogger logger)
    {
        var offer = await offerRepository.GetByIdAsync(command.WaitlistOfferId);
        if (offer is null)
        {
            logger.LogWarning("WaitlistOffer {OfferId} not found, skipping stale rejection", command.WaitlistOfferId);
            return null;
        }

        if (offer.Status != OfferStatus.Pending)
        {
            logger.LogWarning("WaitlistOffer {OfferId} is {Status}, not pending — skipping stale rejection (may have already been handled)", command.WaitlistOfferId, offer.Status);
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
