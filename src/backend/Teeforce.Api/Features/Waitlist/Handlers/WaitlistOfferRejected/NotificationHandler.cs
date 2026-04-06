using Microsoft.Extensions.Logging;
using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.Common;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferRejectedNotificationHandler
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
            logger.LogWarning("Offer {WaitlistOfferId} was never notified, skipping rejection notification", domainEvent.WaitlistOfferId);
            return;
        }

        var entry = await entryRepository.GetRequiredByIdAsync(domainEvent.GolferWaitlistEntryId);

        if (entry.RemovedAt is not null)
        {
            logger.LogWarning("Golfer waitlist entry {EntryId} already removed, skipping rejection notification", domainEvent.GolferWaitlistEntryId);
            return;
        }

        await notificationService.Send(entry.GolferId, new WaitlistOfferExpired(), ct);
    }
}
