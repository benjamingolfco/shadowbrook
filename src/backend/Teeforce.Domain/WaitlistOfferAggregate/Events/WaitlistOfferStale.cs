using Teeforce.Domain.Common;

namespace Teeforce.Domain.WaitlistOfferAggregate.Events;

public record WaitlistOfferStale : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid OpeningId { get; init; }
}
