namespace Shadowbrook.Api.Models;

public class CourseWaitlist
{
    public Guid Id { get; set; }
    public required Guid CourseId { get; set; }
    public required DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Course? Course { get; set; }
    public ICollection<WaitlistRequest> WaitlistRequests { get; set; } = new List<WaitlistRequest>();
}
