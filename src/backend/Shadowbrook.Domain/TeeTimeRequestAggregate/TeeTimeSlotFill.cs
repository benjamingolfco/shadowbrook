using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public class TeeTimeSlotFill : Entity
{
    public Guid TeeTimeRequestId { get; private set; }
    public Guid GolferId { get; private set; }
    public Guid BookingId { get; private set; }
    public int GroupSize { get; private set; }
    public DateTimeOffset FilledAt { get; private set; }

    private TeeTimeSlotFill() { } // EF

    internal TeeTimeSlotFill(Guid teeTimeRequestId, Guid golferId, Guid bookingId, int groupSize)
    {
        Id = Guid.CreateVersion7();
        TeeTimeRequestId = teeTimeRequestId;
        GolferId = golferId;
        BookingId = bookingId;
        GroupSize = groupSize;
        FilledAt = DateTimeOffset.UtcNow;
    }
}
