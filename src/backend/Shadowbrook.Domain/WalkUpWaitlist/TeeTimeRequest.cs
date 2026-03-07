using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist;

public class TeeTimeRequest : Entity
{
    public Guid WalkUpWaitlistId { get; private set; }
    public TimeOnly TeeTime { get; private set; }
    public int GolfersNeeded { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TeeTimeRequest() { } // EF

    public TeeTimeRequest(Guid walkUpWaitlistId, TimeOnly teeTime, int golfersNeeded)
    {
        this.Id = Guid.NewGuid();
        this.WalkUpWaitlistId = walkUpWaitlistId;
        this.TeeTime = teeTime;
        this.GolfersNeeded = golfersNeeded;
        this.Status = RequestStatus.Pending;
        this.CreatedAt = DateTimeOffset.UtcNow;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
