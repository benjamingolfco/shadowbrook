using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WaitlistOfferAggregate.Events;

public record WaitlistOfferAccepted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid OpeningId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid GolferId { get; init; }
    public required int GroupSize { get; init; }
}
