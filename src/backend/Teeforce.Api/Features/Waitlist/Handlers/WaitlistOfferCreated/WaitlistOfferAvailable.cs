using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferAvailable(string CourseName, TimeOnly Time, string ClaimUrl) : INotification;

public class WaitlistOfferAvailableSmsFormatter : SmsFormatter<WaitlistOfferAvailable>
{
    protected override string FormatMessage(WaitlistOfferAvailable n) =>
        $"{n.CourseName}: {n.Time:h:mm tt} tee time available! Claim your spot: {n.ClaimUrl}";
}
