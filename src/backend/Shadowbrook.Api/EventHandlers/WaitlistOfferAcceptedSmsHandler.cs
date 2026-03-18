using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public static class WaitlistOfferAcceptedSmsHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted domainEvent,
        IGolferWaitlistEntryRepository entryRepository,
        IGolferRepository golferRepository,
        ITextMessageService textMessageService,
        CancellationToken ct)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId);
        if (golfer is null)
        {
            return;
        }

        var message = "We're processing your tee time request — you'll receive a confirmation shortly.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
