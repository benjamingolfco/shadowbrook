using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOpeningAggregate.Events;

public record TeeTimeOpeningExpired : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid OpeningId { get; init; }
}
