using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate;

public class TeeTimeClaim : Entity
{
    public Guid TeeTimeId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid GolferId { get; private set; }
    public int GroupSize { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }

    private TeeTimeClaim() { } // EF

    internal TeeTimeClaim(Guid teeTimeId, Guid bookingId, Guid golferId, int groupSize, DateTimeOffset claimedAt)
    {
        Id = Guid.CreateVersion7();
        TeeTimeId = teeTimeId;
        BookingId = bookingId;
        GolferId = golferId;
        GroupSize = groupSize;
        ClaimedAt = claimedAt;
    }
}
