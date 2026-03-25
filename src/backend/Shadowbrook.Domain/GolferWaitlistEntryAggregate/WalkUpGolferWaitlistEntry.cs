namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public class WalkUpGolferWaitlistEntry : GolferWaitlistEntry
{
    private WalkUpGolferWaitlistEntry() { } // EF

    internal WalkUpGolferWaitlistEntry(
        Guid courseWaitlistId,
        Guid golferId,
        int groupSize,
        TimeOnly windowStart,
        TimeOnly windowEnd)
        : base(courseWaitlistId, golferId, groupSize, isWalkUp: true, windowStart, windowEnd)
    {
    }

    public void ExtendWindow(TimeOnly newEnd)
    {
        WindowEnd = newEnd;
        AddDomainEvent(new Events.WalkUpEntryWindowExtended
        {
            GolferWaitlistEntryId = Id,
            NewEnd = newEnd
        });
    }
}
