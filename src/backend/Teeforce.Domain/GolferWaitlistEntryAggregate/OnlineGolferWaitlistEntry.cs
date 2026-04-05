namespace Teeforce.Domain.GolferWaitlistEntryAggregate;

public class OnlineGolferWaitlistEntry : GolferWaitlistEntry
{
    private OnlineGolferWaitlistEntry() { } // EF

    internal OnlineGolferWaitlistEntry(
        Guid courseWaitlistId,
        Guid golferId,
        int groupSize,
        DateTime windowStart,
        DateTime windowEnd,
        DateTimeOffset now)
        : base(courseWaitlistId, golferId, groupSize, isWalkUp: false, windowStart, windowEnd, now)
    {
    }
}
