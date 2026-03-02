namespace Shadowbrook.Api.Models;

/// <summary>
/// A 4-digit daily code that allows walk-up golfers to identify and join a course's waitlist.
/// Codes are globally unique per day. Created by course operators via the pro shop UI.
/// </summary>
public class WalkUpCode
{
    public Guid Id { get; set; }
    public required Guid CourseId { get; set; }
    public required string Code { get; set; }      // 4-digit numeric string, e.g. "4829"
    public required DateOnly Date { get; set; }    // The day this code is valid
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Course? Course { get; set; }
}
