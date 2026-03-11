using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

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
        Id = Guid.CreateVersion7();
        WalkUpWaitlistId = walkUpWaitlistId;
        TeeTime = teeTime;
        GolfersNeeded = golfersNeeded;
        Status = RequestStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Fulfill()
    {
        if (Status != RequestStatus.Pending)
        {
            throw new TeeTimeRequestNotPendingException(Status);
        }

        Status = RequestStatus.Fulfilled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
