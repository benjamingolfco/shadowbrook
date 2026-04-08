using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeBlocked : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required Guid TeeTimeId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
    public required string Reason { get; init; }
}
