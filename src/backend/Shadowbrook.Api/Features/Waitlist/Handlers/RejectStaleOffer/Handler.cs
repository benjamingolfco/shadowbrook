using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
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
        var offer = await offerRepository.GetRequiredByIdAsync(command.WaitlistOfferId);

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
