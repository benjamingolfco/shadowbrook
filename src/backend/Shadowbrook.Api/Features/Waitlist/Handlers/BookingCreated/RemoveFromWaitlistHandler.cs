using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferAcceptedRemoveFromWaitlistHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        IGolferWaitlistEntryRepository entryRepository)
    {
        var entry = await entryRepository.GetByIdAsync(evt.GolferWaitlistEntryId)
            ?? throw new InvalidOperationException($"GolferWaitlistEntry {evt.GolferWaitlistEntryId} not found for event {nameof(WaitlistOfferAccepted)}.");

        entry.Remove();
    }
}
