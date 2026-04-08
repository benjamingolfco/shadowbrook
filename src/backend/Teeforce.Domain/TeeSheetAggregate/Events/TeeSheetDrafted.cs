using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Events;

public record TeeSheetDrafted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeSheetId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required int IntervalCount { get; init; }
}
