using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseWaitlistAggregate.Events;

public record WalkUpWaitlistClosed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid CourseWaitlistId { get; init; }
}
