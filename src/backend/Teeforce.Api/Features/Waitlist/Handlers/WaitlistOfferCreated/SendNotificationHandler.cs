using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferCreatedSendNotificationHandler
{
    public static async Task Handle(
        WaitlistOfferCreated evt,
        IWaitlistOfferRepository offerRepository,
        ITeeTimeOpeningRepository openingRepository,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        IOptions<AppSettings> appSettings,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(evt.WaitlistOfferId);
        var opening = await openingRepository.GetRequiredByIdAsync(evt.OpeningId);

        var course = await courseRepository.GetByIdAsync(opening.CourseId);
        var courseName = course?.Name ?? "Course";

        var baseUrl = appSettings.Value.FrontendUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                "App:FrontendUrl is not configured. Notification offer links require a valid frontend URL.");
        }

        var claimUrl = $"{baseUrl}/book/walkup/{offer.Token}";

        await notificationService.Send(evt.GolferId, new WaitlistOfferAvailable(courseName, opening.TeeTime.Time, claimUrl), ct);

        offer.MarkNotified(timeProvider);
    }
}
