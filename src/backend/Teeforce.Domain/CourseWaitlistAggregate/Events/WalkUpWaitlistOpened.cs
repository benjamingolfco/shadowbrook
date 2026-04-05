using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseWaitlistAggregate.Events;

public record WalkUpWaitlistOpened : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid CourseWaitlistId { get; init; }
}
