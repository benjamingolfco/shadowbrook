using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOpeningAggregate.Events;

public record TeeTimeOpeningCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid OpeningId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
    public required int SlotsAvailable { get; init; }
}
