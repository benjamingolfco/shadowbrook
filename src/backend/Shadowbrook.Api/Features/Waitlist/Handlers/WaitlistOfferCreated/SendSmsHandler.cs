using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferCreatedSendSmsHandler
{
    public static async Task Handle(
        WaitlistOfferCreated evt,
        IWaitlistOfferRepository offerRepository,
        ITeeTimeOpeningRepository openingRepository,
        IGolferRepository golferRepository,
        ICourseRepository courseRepository,
        ITextMessageService textMessageService,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetByIdAsync(evt.WaitlistOfferId)
            ?? throw new InvalidOperationException(
                $"WaitlistOffer {evt.WaitlistOfferId} not found for event {nameof(WaitlistOfferCreated)}.");

        var opening = await openingRepository.GetByIdAsync(evt.OpeningId)
            ?? throw new InvalidOperationException(
                $"TeeTimeOpening {evt.OpeningId} not found for event {nameof(WaitlistOfferCreated)}.");

        var golfer = await golferRepository.GetByIdAsync(evt.GolferId)
            ?? throw new InvalidOperationException(
                $"Golfer {evt.GolferId} not found for event {nameof(WaitlistOfferCreated)}.");

        var course = await courseRepository.GetByIdAsync(opening.CourseId);
        var courseName = course?.Name ?? "Course";

        var baseUrl = configuration["App:FrontendUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                "App:FrontendUrl is not configured. SMS offer links require a valid frontend URL.");
        }

        var message =
            $"{courseName}: {opening.TeeTime:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
        await textMessageService.SendAsync(golfer.Phone, message, ct);

        offer.MarkNotified();
    }
}
