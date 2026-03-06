namespace Shadowbrook.Api.Models;

public class CourseWaitlist
{
    public Guid Id { get; set; }
    public required Guid CourseId { get; set; }
    public required DateOnly Date { get; set; }
    public required string ShortCode { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Course? Course { get; set; }
    public ICollection<WaitlistRequest> WaitlistRequests { get; set; } = new List<WaitlistRequest>();
    public ICollection<GolferWaitlistEntry> GolferWaitlistEntries { get; set; } = new List<GolferWaitlistEntry>();
}
