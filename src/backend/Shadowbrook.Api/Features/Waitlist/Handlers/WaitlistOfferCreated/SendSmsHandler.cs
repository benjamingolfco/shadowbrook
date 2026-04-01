using Microsoft.Extensions.Options;
using Shadowbrook.Api.Infrastructure.Configuration;
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
        IOptions<AppSettings> appSettings,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(evt.WaitlistOfferId);
        var opening = await openingRepository.GetRequiredByIdAsync(evt.OpeningId);
        var golfer = await golferRepository.GetRequiredByIdAsync(evt.GolferId);

        var course = await courseRepository.GetByIdAsync(opening.CourseId);
        var courseName = course?.Name ?? "Course";

        var baseUrl = appSettings.Value.FrontendUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                "App:FrontendUrl is not configured. SMS offer links require a valid frontend URL.");
        }

        var message =
            $"{courseName}: {opening.TeeTime.Time:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
        await textMessageService.SendAsync(golfer.Phone, message, ct);

        offer.MarkNotified(timeProvider);
    }
}
