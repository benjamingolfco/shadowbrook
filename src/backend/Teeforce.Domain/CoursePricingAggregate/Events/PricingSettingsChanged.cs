using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate.Events;

public record PricingSettingsChanged : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required Guid CourseId { get; init; }
}
