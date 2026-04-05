using Teeforce.Domain.Common;

namespace Teeforce.Domain.WaitlistOfferAggregate.Events;

public record WaitlistOfferCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid OpeningId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid GolferId { get; init; }
    public required int GroupSize { get; init; }
    public required bool IsWalkUp { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
}
