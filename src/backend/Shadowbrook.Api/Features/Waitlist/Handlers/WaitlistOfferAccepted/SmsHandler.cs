using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferAcceptedSmsHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted domainEvent,
        IGolferWaitlistEntryRepository entryRepository,
        IGolferRepository golferRepository,
        ITextMessageService textMessageService,
        CancellationToken ct)
    {
        var entry = await entryRepository.GetRequiredByIdAsync(domainEvent.GolferWaitlistEntryId);
        var golfer = await golferRepository.GetRequiredByIdAsync(entry.GolferId);

        var message = "We're processing your tee time request — you'll receive a confirmation shortly.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
