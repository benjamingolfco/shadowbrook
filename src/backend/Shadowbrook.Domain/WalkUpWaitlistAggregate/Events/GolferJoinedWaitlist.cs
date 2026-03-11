using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlistAggregate.Events;

public record GolferJoinedWaitlist : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid CourseWaitlistId { get; init; }
    public required Guid GolferId { get; init; }
    public required string GolferName { get; init; }
    public required string GolferPhone { get; init; }
    public required Guid CourseId { get; init; }
    public required int Position { get; init; }
}
