using Teeforce.Domain.Common;

namespace Teeforce.Domain.GolferWaitlistEntryAggregate.Events;

public record WalkUpEntryWindowExtended : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid GolferWaitlistEntryId { get; init; }
    public required DateTime NewEnd { get; init; }
}
