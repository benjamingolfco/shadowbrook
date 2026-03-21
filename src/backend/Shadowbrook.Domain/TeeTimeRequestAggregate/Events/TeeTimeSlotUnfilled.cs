using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

public record TeeTimeSlotUnfilled : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
}
