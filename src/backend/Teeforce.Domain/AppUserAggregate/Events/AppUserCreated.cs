using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Events;

public record AppUserCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AppUserId { get; init; }
    public required string Email { get; init; }
    public required AppUserRole Role { get; init; }
}
