using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

public record TeeTimeRequestClosed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeTimeRequestId { get; init; }
}
