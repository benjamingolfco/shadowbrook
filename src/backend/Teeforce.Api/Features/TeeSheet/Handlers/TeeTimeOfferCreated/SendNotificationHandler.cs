using Microsoft.Extensions.Logging;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate;
using Teeforce.Domain.TeeTimeOfferAggregate.Events;

namespace Teeforce.Api.Features.TeeSheet.Handlers;

public static class TeeTimeOfferCreatedSendNotificationHandler
{
    public static async Task Handle(
        TeeTimeOfferCreated evt,
        ITeeTimeOfferRepository offerRepository,
        ITimeProvider timeProvider,
        ILogger logger)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(evt.TeeTimeOfferId);
        offer.MarkNotified(timeProvider);

        logger.LogInformation(
            "TeeTimeOffer {OfferId} for TeeTime {TeeTimeId} sent to Golfer {GolferId}",
            evt.TeeTimeOfferId, evt.TeeTimeId, evt.GolferId);
    }
}
