using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Exceptions;

namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public class WalkUpGolferWaitlistEntry : GolferWaitlistEntry
{
    private WalkUpGolferWaitlistEntry() { } // EF

    internal WalkUpGolferWaitlistEntry(
        Guid courseWaitlistId,
        Guid golferId,
        int groupSize,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        DateTimeOffset now)
        : base(courseWaitlistId, golferId, groupSize, isWalkUp: true, windowStart, windowEnd, now)
    {
    }

    public void ExtendWindow(TimeOnly newEnd)
    {
        if (RemovedAt is not null)
        {
            throw new CannotExtendRemovedEntryException();
        }

        WindowEnd = newEnd;
        AddDomainEvent(new Events.WalkUpEntryWindowExtended
        {
            GolferWaitlistEntryId = Id,
            NewEnd = newEnd
        });
    }
}
