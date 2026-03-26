using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferRejectedSmsHandler
{
    public static async Task Handle(
        WaitlistOfferRejected domainEvent,
        IWaitlistOfferRepository offerRepository,
        IGolferWaitlistEntryRepository entryRepository,
        IGolferRepository golferRepository,
        ITextMessageService textMessageService,
        ILogger logger,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetByIdAsync(domainEvent.WaitlistOfferId);
        if (offer is null)
        {
            logger.LogWarning("WaitlistOffer {OfferId} not found, skipping rejection SMS", domainEvent.WaitlistOfferId);
            return;
        }

        if (offer.NotifiedAt is null)
        {
            // Golfer was never texted about this offer — no SMS to send
            return;
        }

        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            logger.LogWarning("GolferWaitlistEntry {EntryId} not found, skipping rejection SMS for offer {OfferId}", domainEvent.GolferWaitlistEntryId, domainEvent.WaitlistOfferId);
            return;
        }

        // Skip if golfer was already removed from the waitlist
        if (entry.RemovedAt is not null)
        {
            logger.LogWarning("GolferWaitlistEntry {EntryId} already removed, skipping rejection SMS for offer {OfferId}", entry.Id, domainEvent.WaitlistOfferId);
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId);
        if (golfer is null)
        {
            logger.LogWarning("Golfer {GolferId} not found for waitlist entry {EntryId}, skipping rejection SMS for offer {OfferId}", entry.GolferId, entry.Id, domainEvent.WaitlistOfferId);
            return;
        }

        var message = "Sorry, that tee time is no longer available.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
