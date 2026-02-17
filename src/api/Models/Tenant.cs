namespace Shadowbrook.Api.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public required string OrganizationName { get; set; }
    public required string ContactName { get; set; }
    public required string ContactEmail { get; set; }
    public required string ContactPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
