using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOfferAggregate.Events;

public record TeeTimeOfferRejected : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required Guid TeeTimeOfferId { get; init; }
    public required Guid TeeTimeId { get; init; }
    public required string Reason { get; init; }
}
