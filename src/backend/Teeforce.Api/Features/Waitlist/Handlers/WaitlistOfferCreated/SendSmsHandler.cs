using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferNotification(string CourseName, TimeOnly TeeTime, Guid Token, string BaseUrl) : INotification;

public class WaitlistOfferNotificationSmsFormatter : SmsFormatter<WaitlistOfferNotification>
{
    protected override string FormatMessage(WaitlistOfferNotification n) =>
        $"{n.CourseName}: {n.TeeTime:h:mm tt} tee time available! Claim your spot: {n.BaseUrl}/book/walkup/{n.Token}";
}

public static class WaitlistOfferCreatedSendSmsHandler
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
                "App:FrontendUrl is not configured. SMS offer links require a valid frontend URL.");
        }

        await notificationService.Send(evt.GolferId, new WaitlistOfferNotification(courseName, opening.TeeTime.Time, offer.Token, baseUrl), ct);

        offer.MarkNotified(timeProvider);
    }
}
