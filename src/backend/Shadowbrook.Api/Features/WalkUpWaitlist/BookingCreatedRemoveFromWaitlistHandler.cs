using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public static class WaitlistOfferAcceptedRemoveFromWaitlistHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        IGolferWaitlistEntryRepository entryRepository)
    {
        var entry = await entryRepository.GetByIdAsync(evt.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        entry.Remove();
    }
}
