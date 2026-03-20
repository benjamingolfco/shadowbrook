using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;

namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public class GolferWaitlistEntry : Entity
{
    public Guid CourseWaitlistId { get; private set; }
    public Guid GolferId { get; private set; }
    public bool IsWalkUp { get; private set; }
    public bool IsReady { get; private set; }
    public int GroupSize { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private GolferWaitlistEntry() { } // EF

    public GolferWaitlistEntry(Guid courseWaitlistId, Guid golferId, int groupSize = 1)
    {
        var now = DateTimeOffset.UtcNow;
        Id = Guid.CreateVersion7();
        CourseWaitlistId = courseWaitlistId;
        GolferId = golferId;
        IsWalkUp = true;
        IsReady = true;
        GroupSize = groupSize;
        JoinedAt = now;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void Remove()
    {
        var now = DateTimeOffset.UtcNow;
        RemovedAt = now;
        UpdatedAt = now;

        AddDomainEvent(new GolferRemovedFromWaitlist
        {
            GolferWaitlistEntryId = Id,
            GolferId = GolferId
        });
    }
}
