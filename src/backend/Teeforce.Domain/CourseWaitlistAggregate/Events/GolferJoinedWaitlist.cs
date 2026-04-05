using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseWaitlistAggregate.Events;

public record GolferJoinedWaitlist : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid CourseWaitlistId { get; init; }
    public required Guid GolferId { get; init; }
}
