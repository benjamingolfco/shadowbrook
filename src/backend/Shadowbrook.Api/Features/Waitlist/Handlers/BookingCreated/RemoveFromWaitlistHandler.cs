using Microsoft.Extensions.Logging;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferAcceptedRemoveFromWaitlistHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        IGolferWaitlistEntryRepository entryRepository,
        ILogger logger)
    {
        var entry = await entryRepository.GetByIdAsync(evt.GolferWaitlistEntryId);
        if (entry is null)
        {
            logger.LogWarning("GolferWaitlistEntry {EntryId} not found, skipping waitlist removal for offer {OfferId}", evt.GolferWaitlistEntryId, evt.WaitlistOfferId);
            return;
        }

        entry.Remove();
    }
}
