using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public class WaitlistOfferRejectedSmsHandler(
    IGolferWaitlistEntryRepository entryRepository,
    IGolferRepository golferRepository,
    ITextMessageService textMessageService)
    : IDomainEventHandler<WaitlistOfferRejected>
{
    public async Task HandleAsync(WaitlistOfferRejected domainEvent, CancellationToken ct = default)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        // Skip if golfer was already removed from the waitlist
        if (entry.RemovedAt is not null)
        {
            return;
        }

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId);
        if (golfer is null)
        {
            return;
        }

        var message = "Sorry, that tee time is no longer available.";
        await textMessageService.SendAsync(golfer.Phone, message, ct);
    }
}
