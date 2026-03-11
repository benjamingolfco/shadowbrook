using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Events;

public record WaitlistOfferAccepted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required string GolferPhone { get; init; }
    public required string CourseName { get; init; }
    public required TimeOnly TeeTime { get; init; }
    public required DateOnly OfferDate { get; init; }
}
