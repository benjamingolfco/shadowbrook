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
        var offer = await offerRepository.GetByIdAsync(domainEvent.WaitlistOfferId)
            ?? throw new InvalidOperationException($"WaitlistOffer {domainEvent.WaitlistOfferId} not found for event {nameof(WaitlistOfferRejected)}.");

        if (offer.NotifiedAt is null)
        {
            // Golfer was never texted about this offer — no SMS to send
            return;
        }

        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId)
            ?? throw new InvalidOperationException($"GolferWaitlistEntry {domainEvent.GolferWaitlistEntryId} not found for event {nameof(WaitlistOfferRejected)}.");

        // Skip if golfer was already removed from the waitlist
        if (entry.RemovedAt is not null)
        {
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId)
            ?? throw new InvalidOperationException($"Golfer {entry.GolferId} not found for waitlist entry {entry.Id}.");

        var message = "Sorry, that tee time is no longer available.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
