using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeClaimed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required Guid TeeTimeId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
    public required int GroupSize { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
