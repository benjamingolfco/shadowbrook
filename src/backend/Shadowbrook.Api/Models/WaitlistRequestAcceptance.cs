namespace Shadowbrook.Api.Models;

public class WaitlistRequestAcceptance
{
    public Guid Id { get; set; }
    public Guid WaitlistRequestId { get; set; }
    public Guid GolferWaitlistEntryId { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
