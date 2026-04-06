using Teeforce.Domain.Common;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferRejectedSmsHandler
{
    public static async Task Handle(
        WaitlistOfferRejected domainEvent,
        IWaitlistOfferRepository offerRepository,
        IGolferWaitlistEntryRepository entryRepository,
        INotificationService notificationService,
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

        var message = "Sorry, that tee time is no longer available.";
        await notificationService.Send(entry.GolferId, message, ct);
    }
}
