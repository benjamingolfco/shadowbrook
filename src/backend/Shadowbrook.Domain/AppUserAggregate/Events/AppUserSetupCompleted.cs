using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate.Events;

public record AppUserSetupCompleted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AppUserId { get; init; }
    public required string Email { get; init; }
}
