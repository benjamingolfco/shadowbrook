using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public record MarkOfferStale(Guid WaitlistOfferId, Guid OpeningId);

public static class MarkOfferStaleHandler
{
    public static async Task Handle(
        MarkOfferStale command,
        IWaitlistOfferRepository offerRepository,
        ILogger logger)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(command.WaitlistOfferId);

        if (offer.Status != OfferStatus.Pending)
        {
            logger.LogWarning(
                "WaitlistOffer {OfferId} is {Status}, not pending — skipping stale marking",
                command.WaitlistOfferId,
                offer.Status);
            return;
        }

        offer.MarkStale();
    }
}
