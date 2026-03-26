namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public class OnlineGolferWaitlistEntry : GolferWaitlistEntry
{
    private OnlineGolferWaitlistEntry() { } // EF

    internal OnlineGolferWaitlistEntry(
        Guid courseWaitlistId,
        Guid golferId,
        int groupSize,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        DateTimeOffset now)
        : base(courseWaitlistId, golferId, groupSize, isWalkUp: false, windowStart, windowEnd, now)
    {
    }
}
