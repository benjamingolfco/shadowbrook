using Microsoft.Extensions.Logging;
using Teeforce.Api.Features.Waitlist.Policies;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistServices;

namespace Teeforce.Api.Features.Waitlist.Handlers;

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
        var opening = await openingRepository.GetRequiredByIdAsync(command.OpeningId);

        if (!opening.IsOpen)
        {
            logger.LogWarning("Opening {OpeningId} is not open (status: {Status}), skipping offer matching", command.OpeningId, opening.Status);
            return;
        }

        var eligibleEntries = await matchingService.FindEligibleEntriesAsync(opening, ct);

        if (eligibleEntries.Count == 0)
        {
            logger.LogInformation(
                "No eligible golfers found for opening {OpeningId}, skipping offer dispatch",
                command.OpeningId);
            return;
        }

        foreach (var entry in eligibleEntries.Take(command.MaxOffers))
        {
            var offer = entry.CreateOffer(opening, timeProvider);
            offerRepository.Add(offer);
        }
    }
}
