namespace Shadowbrook.Api.Models;

public class Booking
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public required string GolferName { get; set; }
    public int PlayerCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Course? Course { get; set; }
}
