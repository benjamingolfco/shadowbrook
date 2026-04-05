namespace Teeforce.Domain.TeeTimeOpeningAggregate;

public class ClaimedSlot
{
    public Guid BookingId { get; private set; }
    public Guid GolferId { get; private set; }
    public int GroupSize { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }

    private ClaimedSlot() { } // EF

    public ClaimedSlot(Guid bookingId, Guid golferId, int groupSize, DateTimeOffset claimedAt)
    {
        BookingId = bookingId;
        GolferId = golferId;
        GroupSize = groupSize;
        ClaimedAt = claimedAt;
    }
}
