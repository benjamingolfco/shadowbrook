using Microsoft.Extensions.Logging;
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
        ILogger logger,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(domainEvent.WaitlistOfferId);

        if (offer.NotifiedAt is null)
        {
            logger.LogWarning("Offer {WaitlistOfferId} was never notified, skipping rejection SMS", domainEvent.WaitlistOfferId);
            return;
        }

        var entry = await entryRepository.GetRequiredByIdAsync(domainEvent.GolferWaitlistEntryId);

        if (entry.RemovedAt is not null)
        {
            logger.LogWarning("Golfer waitlist entry {EntryId} already removed, skipping rejection SMS", domainEvent.GolferWaitlistEntryId);
            return;
        }

        var message = "Sorry, that tee time is no longer available.";
        await notificationService.Send(entry.GolferId, message, ct);
    }
}
