using Teeforce.Domain.Common;

namespace Teeforce.Domain.BookingAggregate.Events;

public record BookingCancelled : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid BookingId { get; init; }
    public required BookingStatus PreviousStatus { get; init; }
}
