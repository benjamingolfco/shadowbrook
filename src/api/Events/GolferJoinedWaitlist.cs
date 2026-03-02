namespace Shadowbrook.Api.Events;

public class GolferJoinedWaitlist : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid GolferId { get; init; }
    public required string GolferPhone { get; init; }
    public required string GolferFirstName { get; init; }
    public required string CourseName { get; init; }
    public required int Position { get; init; }
}
