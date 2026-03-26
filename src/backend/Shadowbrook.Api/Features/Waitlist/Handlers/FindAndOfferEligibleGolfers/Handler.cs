using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public record FindAndOfferEligibleGolfers(Guid OpeningId, int MaxOffers);

public static class FindAndOfferEligibleGolfersHandler
{
    public static async Task Handle(
        FindAndOfferEligibleGolfers command,
        ITeeTimeOpeningRepository openingRepository,
        WaitlistMatchingService matchingService,
        IWaitlistOfferRepository offerRepository,
        ITimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var opening = await openingRepository.GetByIdAsync(command.OpeningId)
            ?? throw new InvalidOperationException(
                $"TeeTimeOpening {command.OpeningId} not found for command {nameof(FindAndOfferEligibleGolfers)}.");

        var eligibleEntries = await matchingService.FindEligibleEntriesAsync(opening, ct);

        var offersToCreate = Math.Min(eligibleEntries.Count, command.MaxOffers);
        if (offersToCreate == 0)
        {
            logger.LogWarning(
                "No eligible golfers found for opening {OpeningId}, skipping offer dispatch",
                command.OpeningId);
            return;
        }

        for (var i = 0; i < offersToCreate; i++)
        {
            var entry = eligibleEntries[i];
            var offer = entry.CreateOffer(opening, timeProvider);
            offerRepository.Add(offer);
        }
    }
}
