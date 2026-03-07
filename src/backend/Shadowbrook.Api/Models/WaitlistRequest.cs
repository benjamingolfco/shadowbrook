namespace Shadowbrook.Api.Models;

public class WaitlistRequest
{
    public Guid Id { get; set; }
    public required Guid CourseWaitlistId { get; set; }
    public required TimeOnly TeeTime { get; set; }
    public required int GolfersNeeded { get; set; }
    public required string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public CourseWaitlist? CourseWaitlist { get; set; }
}
