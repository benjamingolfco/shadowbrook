using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.BookingAggregate.Events;

public record BookingRejected : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid BookingId { get; init; }
}
