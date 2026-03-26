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
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId)
            ?? throw new InvalidOperationException(
                $"GolferWaitlistEntry {domainEvent.GolferWaitlistEntryId} not found for accepted offer.");

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId)
            ?? throw new InvalidOperationException(
                $"Golfer {entry.GolferId} not found for waitlist entry {entry.Id}.");

        var message = "We're processing your tee time request — you'll receive a confirmation shortly.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
