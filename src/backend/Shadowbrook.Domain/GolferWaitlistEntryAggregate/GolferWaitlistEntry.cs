using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public abstract class GolferWaitlistEntry : Entity
{
    public Guid CourseWaitlistId { get; private set; }
    public Guid GolferId { get; private set; }
    public bool IsWalkUp { get; protected set; }
    public int GroupSize { get; private set; }
    public DateTime WindowStart { get; protected set; }
    public DateTime WindowEnd { get; protected set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    protected GolferWaitlistEntry() { } // EF

    protected internal GolferWaitlistEntry(
        Guid courseWaitlistId,
        Guid golferId,
        int groupSize,
        bool isWalkUp,
        DateTime windowStart,
        DateTime windowEnd,
        DateTimeOffset now)
    {
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

    public WaitlistOffer CreateOffer(TeeTimeOpening opening, ITimeProvider timeProvider) =>
        WaitlistOffer.Create(opening.Id, Id, GolferId, GroupSize, IsWalkUp, opening.CourseId, opening.TeeTime.Date, opening.TeeTime.Time, timeProvider);

    public void Remove(ITimeProvider timeProvider)
    {
        if (RemovedAt is not null)
        {
            return;
        }

        RemovedAt = timeProvider.GetCurrentTimestamp();

        AddDomainEvent(new GolferRemovedFromWaitlist
        {
            GolferWaitlistEntryId = Id,
            GolferId = GolferId
        });
    }
}
