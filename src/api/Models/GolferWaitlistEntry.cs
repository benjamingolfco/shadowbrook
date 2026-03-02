namespace Shadowbrook.Api.Models;

/// <summary>
/// A golfer's presence on a course's daily waitlist.
/// Represents "I am available to play at this course today."
/// GolferName and GolferPhone are denormalized copies — always populated for SMS delivery
/// and operator display without requiring a join to the Golfer table.
/// </summary>
public class GolferWaitlistEntry
{
    public Guid Id { get; set; }
    public required Guid CourseWaitlistId { get; set; }
    public required Guid GolferId { get; set; }
    public required string GolferName { get; set; }    // Denormalized: "FirstName LastName"
    public required string GolferPhone { get; set; }   // Denormalized: E.164
    public bool IsWalkUp { get; set; }
    public bool IsReady { get; set; }
    public required DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }     // null = active
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public CourseWaitlist? CourseWaitlist { get; set; }
    public Golfer? Golfer { get; set; }
}
