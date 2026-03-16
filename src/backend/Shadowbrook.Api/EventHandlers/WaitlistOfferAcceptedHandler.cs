using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

// TODO: Rewrite in later task — handler body will use BookingId and GolferId from lean event
public class WaitlistOfferAcceptedHandler
    : IDomainEventHandler<WaitlistOfferAccepted>
{
    public Task HandleAsync(WaitlistOfferAccepted domainEvent, CancellationToken ct = default)
    {
        // Placeholder — full implementation pending
        return Task.CompletedTask;
    }
}
