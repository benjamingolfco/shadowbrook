using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
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
        IGolferRepository golferRepository,
        ICourseRepository courseRepository,
        ITextMessageService textMessageService,
        ITimeProvider timeProvider,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        var opening = await openingRepository.GetByIdAsync(command.OpeningId)
            ?? throw new InvalidOperationException(
                $"TeeTimeOpening {command.OpeningId} not found for command {nameof(FindAndOfferEligibleGolfers)}.");

        if (opening.Status != TeeTimeOpeningStatus.Open)
        {
            logger.LogWarning(
                "TeeTimeOpening {OpeningId} is {Status}, not open — skipping offer dispatch",
                command.OpeningId, opening.Status);
            return;
        }

        var eligibleEntries = await matchingService.FindEligibleEntriesAsync(opening, ct);

        var offersToCreate = Math.Min(eligibleEntries.Count, command.MaxOffers);
        if (offersToCreate == 0)
        {
            logger.LogWarning(
                "No eligible golfers found for opening {OpeningId}, skipping offer dispatch",
                command.OpeningId);
            return;
        }

        var baseUrl = configuration["App:FrontendUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                "App:FrontendUrl is not configured. SMS offer links require a valid frontend URL.");
        }

        var course = await courseRepository.GetByIdAsync(opening.CourseId);
        var courseName = course?.Name ?? "Course";

        for (var i = 0; i < offersToCreate; i++)
        {
            var entry = eligibleEntries[i];

            var golfer = await golferRepository.GetByIdAsync(entry.GolferId)
                ?? throw new InvalidOperationException(
                    $"Golfer {entry.GolferId} not found for waitlist entry {entry.Id}.");

            var offer = await entry.SendOfferAsync(
                opening, golfer, textMessageService, timeProvider, courseName, baseUrl, ct);
            offerRepository.Add(offer);
        }
    }
}
