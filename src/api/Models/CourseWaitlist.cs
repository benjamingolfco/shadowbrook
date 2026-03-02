namespace Shadowbrook.Api.Models;

/// <summary>
/// Daily waitlist container for a course. One per course per day.
/// Created lazily when the first entry or request is added for that course-date pair.
/// </summary>
public class CourseWaitlist
{
    public Guid Id { get; set; }
    public required Guid CourseId { get; set; }
    public required DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Course? Course { get; set; }
}
