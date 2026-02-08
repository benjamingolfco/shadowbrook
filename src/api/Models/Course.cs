namespace Shadowbrook.Api.Models;

public class Course
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int? TeeTimeIntervalMinutes { get; set; }
    public TimeOnly? FirstTeeTime { get; set; }
    public TimeOnly? LastTeeTime { get; set; }
    public decimal? FlatRatePrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
