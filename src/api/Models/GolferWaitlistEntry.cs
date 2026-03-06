namespace Shadowbrook.Api.Models;

public class GolferWaitlistEntry
{
    public Guid Id { get; set; }
    public required Guid CourseWaitlistId { get; set; }
    public required Guid GolferId { get; set; }
    public required string GolferName { get; set; }
    public required string GolferPhone { get; set; }
    public bool IsWalkUp { get; set; } = true;
    public bool IsReady { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public CourseWaitlist? CourseWaitlist { get; set; }
    public Golfer? Golfer { get; set; }
}
