using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Api.Features.Waitlist;

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
        CancellationToken ct)
    {
        var opening = await openingRepository.GetByIdAsync(command.OpeningId);
        if (opening is null || opening.Status != TeeTimeOpeningStatus.Open)
        {
            return;
        }

        var eligibleEntries = await matchingService.FindEligibleEntriesAsync(opening, ct);

        var offersToCreate = Math.Min(eligibleEntries.Count, command.MaxOffers);
        if (offersToCreate == 0)
        {
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

            var offer = WaitlistOffer.Create(
                opening.Id, entry.Id, entry.GolferId, entry.GroupSize, entry.IsWalkUp, timeProvider);
            offerRepository.Add(offer);

            var golfer = await golferRepository.GetByIdAsync(entry.GolferId);
            if (golfer is null)
            {
                continue;
            }

            var message =
                $"{courseName}: {opening.TeeTime:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
            await textMessageService.SendAsync(golfer.Phone, message, ct);

            offer.MarkNotified();
        }
    }
}
