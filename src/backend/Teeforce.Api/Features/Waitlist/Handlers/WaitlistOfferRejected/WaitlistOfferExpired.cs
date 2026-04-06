using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferExpired : INotification;

public class WaitlistOfferExpiredSmsFormatter : SmsFormatter<WaitlistOfferExpired>
{
    protected override string FormatMessage(WaitlistOfferExpired n) =>
        "Sorry, that tee time is no longer available.";
}
