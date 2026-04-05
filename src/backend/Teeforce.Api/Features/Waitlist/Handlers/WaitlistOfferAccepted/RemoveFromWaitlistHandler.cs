using Teeforce.Domain.Common;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

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
