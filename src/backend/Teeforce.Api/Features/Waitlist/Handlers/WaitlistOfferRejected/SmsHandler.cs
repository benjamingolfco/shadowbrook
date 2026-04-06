using Microsoft.Extensions.Logging;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferRejectedNotification : INotification;

public class WaitlistOfferRejectedNotificationSmsFormatter : SmsFormatter<WaitlistOfferRejectedNotification>
{
    protected override string FormatMessage(WaitlistOfferRejectedNotification n) =>
        "Sorry, that tee time is no longer available.";
}

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

        await notificationService.Send(entry.GolferId, new WaitlistOfferRejectedNotification(), ct);
    }
}
