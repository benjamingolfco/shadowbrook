using Teeforce.Api.Infrastructure.Notifications;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferExpired : INotification;

public class WaitlistOfferExpiredSmsFormatter : ISmsFormatter<WaitlistOfferExpired>
{
    public string Format(WaitlistOfferExpired n) =>
        "Sorry, that tee time is no longer available.";
}
