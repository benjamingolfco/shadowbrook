using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

public record TeeTimeOpeningClaimed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid OpeningId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
}
