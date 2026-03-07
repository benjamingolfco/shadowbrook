namespace Shadowbrook.Api.Events;

public record WaitlistRequestCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistRequestId { get; init; }
    public required Guid CourseWaitlistId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
    public required int GolfersNeeded { get; init; }
}
