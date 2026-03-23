using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlistAggregate.Events;

public record WaitlistReopened : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid CourseWaitlistId { get; init; }
}
