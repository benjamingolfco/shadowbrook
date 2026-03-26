using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.WaitlistOfferAggregate;

public class WaitlistOffer : Entity
{
    public Guid Token { get; private set; }
    public Guid OpeningId { get; private set; }
    public Guid GolferWaitlistEntryId { get; private set; }
    public Guid GolferId { get; private set; }
    public int GroupSize { get; private set; }
    public bool IsWalkUp { get; private set; }
    public OfferStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? NotifiedAt { get; private set; }

    private WaitlistOffer() { } // EF

    public static WaitlistOffer Create(
        Guid openingId,
        Guid golferWaitlistEntryId,
        Guid golferId,
        int groupSize,
        bool isWalkUp,
        ITimeProvider timeProvider)
    {
        var offer = new WaitlistOffer
        {
            Id = Guid.CreateVersion7(),
            Token = Guid.CreateVersion7(),
            OpeningId = openingId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            GolferId = golferId,
            GroupSize = groupSize,
            IsWalkUp = isWalkUp,
            Status = OfferStatus.Pending,
            CreatedAt = timeProvider.GetCurrentTimestamp()
        };

        offer.AddDomainEvent(new WaitlistOfferCreated
        {
            WaitlistOfferId = offer.Id,
            OpeningId = openingId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            GolferId = golferId,
            GroupSize = groupSize,
            IsWalkUp = isWalkUp
        });

        return offer;
    }

    public void MarkNotified()
    {
        if (NotifiedAt is not null)
        {
            throw new OfferAlreadyNotifiedException();
        }

        NotifiedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new WaitlistOfferSent
        {
            WaitlistOfferId = Id,
            OpeningId = OpeningId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferId = GolferId,
            GroupSize = GroupSize,
            IsWalkUp = IsWalkUp
        });
    }

    public void Accept()
    {
        if (Status != OfferStatus.Pending)
        {
            throw new OfferNotPendingException();
        }

        Status = OfferStatus.Accepted;

        AddDomainEvent(new WaitlistOfferAccepted
        {
            WaitlistOfferId = Id,
            OpeningId = OpeningId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferId = GolferId,
            GroupSize = GroupSize
        });
    }

    public void Reject(string reason)
    {
        if (Status != OfferStatus.Pending)
        {
            return; // Idempotent — already resolved
        }

        Status = OfferStatus.Rejected;
        RejectionReason = reason;

        AddDomainEvent(new WaitlistOfferRejected
        {
            WaitlistOfferId = Id,
            OpeningId = OpeningId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            Reason = reason
        });
    }
}
