using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;

namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public abstract class GolferWaitlistEntry : Entity
{
    public Guid CourseWaitlistId { get; private set; }
    public Guid GolferId { get; private set; }
    public bool IsWalkUp { get; protected set; }
    public int GroupSize { get; private set; }
    public TimeOnly WindowStart { get; protected set; }
    public TimeOnly WindowEnd { get; protected set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    protected GolferWaitlistEntry() { } // EF

    protected internal GolferWaitlistEntry(
        Guid courseWaitlistId,
        Guid golferId,
        int groupSize,
        bool isWalkUp,
        TimeOnly windowStart,
        TimeOnly windowEnd)
    {
        var now = DateTimeOffset.UtcNow;
        Id = Guid.CreateVersion7();
        CourseWaitlistId = courseWaitlistId;
        GolferId = golferId;
        IsWalkUp = isWalkUp;
        GroupSize = groupSize;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        JoinedAt = now;
        CreatedAt = now;
    }

    public void Remove()
    {
        if (RemovedAt is not null)
        {
            return;
        }

        RemovedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new GolferRemovedFromWaitlist
        {
            GolferWaitlistEntryId = Id,
            GolferId = GolferId
        });
    }
}
