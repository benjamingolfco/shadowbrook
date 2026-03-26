using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
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
        TimeOnly windowEnd,
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

    public async Task<WaitlistOffer> SendOfferAsync(
        TeeTimeOpening opening,
        Golfer golfer,
        ITextMessageService textMessageService,
        ITimeProvider timeProvider,
        string courseName,
        string claimBaseUrl,
        CancellationToken ct = default)
    {
        var offer = WaitlistOffer.Create(opening.Id, Id, GolferId, GroupSize, IsWalkUp, timeProvider);

        var message =
            $"{courseName}: {opening.TeeTime:h:mm tt} tee time available! Claim your spot: {claimBaseUrl}/book/walkup/{offer.Token}";
        await textMessageService.SendAsync(golfer.Phone, message, ct);

        offer.MarkNotified();

        return offer;
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
