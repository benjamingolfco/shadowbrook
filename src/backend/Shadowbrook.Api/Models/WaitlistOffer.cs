namespace Shadowbrook.Api.Models;

public class WaitlistOffer
{
    public Guid Id { get; set; }
    public Guid Token { get; set; }
    public Guid TeeTimeRequestId { get; set; }
    public Guid GolferWaitlistEntryId { get; set; }
    public Guid CourseId { get; set; }
    public required string CourseName { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly TeeTime { get; set; }
    public int GolfersNeeded { get; set; }
    public required string GolferName { get; set; }
    public required string GolferPhone { get; set; }
    public OfferStatus Status { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
