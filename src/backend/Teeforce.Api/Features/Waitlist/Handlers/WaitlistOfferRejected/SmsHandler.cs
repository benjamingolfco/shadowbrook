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
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(domainEvent.WaitlistOfferId);

        if (offer.NotifiedAt is null)
        {
            // Golfer was never texted about this offer — no SMS to send
            return;
        }

        var entry = await entryRepository.GetRequiredByIdAsync(domainEvent.GolferWaitlistEntryId);

        // Skip if golfer was already removed from the waitlist
        if (entry.RemovedAt is not null)
        {
            return;
        }

        var golfer = await golferRepository.GetRequiredByIdAsync(entry.GolferId);

        var message = "Sorry, that tee time is no longer available.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
