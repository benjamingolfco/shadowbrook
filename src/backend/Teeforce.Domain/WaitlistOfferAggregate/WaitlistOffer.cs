using Teeforce.Domain.Common;
using Teeforce.Domain.WaitlistOfferAggregate.Events;
using Teeforce.Domain.WaitlistOfferAggregate.Exceptions;

namespace Teeforce.Domain.WaitlistOfferAggregate;

public class WaitlistOffer : Entity
{
    public Guid BookingId { get; private set; }
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
    public bool IsStale { get; private set; }
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly TeeTime { get; private set; }

    private WaitlistOffer() { } // EF

    internal static WaitlistOffer Create(
        Guid openingId,
        Guid golferWaitlistEntryId,
        Guid golferId,
        int groupSize,
        bool isWalkUp,
        Guid courseId,
        DateOnly date,
        TimeOnly teeTime,
        ITimeProvider timeProvider)
    {
        var offer = new WaitlistOffer
        {
            Id = Guid.CreateVersion7(),
            BookingId = Guid.CreateVersion7(),
            Token = Guid.CreateVersion7(),
            OpeningId = openingId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            GolferId = golferId,
            GroupSize = groupSize,
            IsWalkUp = isWalkUp,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime,
            Status = OfferStatus.Pending,
            CreatedAt = timeProvider.GetCurrentTimestamp()
        };

        offer.AddDomainEvent(new WaitlistOfferCreated
        {
            WaitlistOfferId = offer.Id,
            BookingId = offer.BookingId,
            OpeningId = openingId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            GolferId = golferId,
            GroupSize = groupSize,
            IsWalkUp = isWalkUp,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime
        });

        return offer;
    }

    public void MarkNotified(ITimeProvider timeProvider)
    {
        if (NotifiedAt is not null)
        {
            throw new OfferAlreadyNotifiedException();
        }

        NotifiedAt = timeProvider.GetCurrentTimestamp();

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

    internal void Accept()
    {
        if (Status != OfferStatus.Pending)
        {
            throw new OfferNotPendingException();
        }

        Status = OfferStatus.Accepted;

        AddDomainEvent(new WaitlistOfferAccepted
        {
            WaitlistOfferId = Id,
            BookingId = BookingId,
            OpeningId = OpeningId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferId = GolferId,
            GroupSize = GroupSize,
            CourseId = CourseId,
            Date = Date,
            TeeTime = TeeTime
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

    public void MarkStale()
    {
        if (Status != OfferStatus.Pending || IsStale)
        {
            return; // Idempotent — already resolved or already stale
        }

        IsStale = true;

        AddDomainEvent(new WaitlistOfferStale
        {
            WaitlistOfferId = Id,
            OpeningId = OpeningId
        });
    }
}
