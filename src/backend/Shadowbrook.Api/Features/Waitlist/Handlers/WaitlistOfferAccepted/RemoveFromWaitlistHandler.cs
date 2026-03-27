using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferAcceptedRemoveFromWaitlistHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        IGolferWaitlistEntryRepository entryRepository,
        ITimeProvider timeProvider)
    {
        var entry = await entryRepository.GetRequiredByIdAsync(evt.GolferWaitlistEntryId);

        entry.Remove(timeProvider);
    }
}
