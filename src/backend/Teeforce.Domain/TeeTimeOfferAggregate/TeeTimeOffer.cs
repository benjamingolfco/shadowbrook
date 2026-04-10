using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOfferAggregate.Events;
using Teeforce.Domain.TeeTimeOfferAggregate.Exceptions;

namespace Teeforce.Domain.TeeTimeOfferAggregate;

public class TeeTimeOffer : Entity
{
    public Guid TeeTimeId { get; private set; }
    public Guid GolferWaitlistEntryId { get; private set; }
    public Guid GolferId { get; private set; }
    public int GroupSize { get; private set; }
    public Guid Token { get; private set; }
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public TeeTimeOfferStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public bool IsStale { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? NotifiedAt { get; private set; }

    private TeeTimeOffer() { } // EF

    public static TeeTimeOffer Create(
        Guid teeTimeId,
        Guid golferWaitlistEntryId,
        Guid golferId,
        int groupSize,
        Guid courseId,
        DateOnly date,
        TimeOnly time,
        ITimeProvider timeProvider)
    {
        var offer = new TeeTimeOffer
        {
            Id = Guid.CreateVersion7(),
            TeeTimeId = teeTimeId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            GolferId = golferId,
            GroupSize = groupSize,
            Token = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            Time = time,
            Status = TeeTimeOfferStatus.Pending,
            CreatedAt = timeProvider.GetCurrentTimestamp(),
        };

        offer.AddDomainEvent(new TeeTimeOfferCreated
        {
            TeeTimeOfferId = offer.Id,
            TeeTimeId = teeTimeId,
            GolferId = golferId,
            GroupSize = groupSize,
            CourseId = courseId,
            Date = date,
            Time = time,
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

        AddDomainEvent(new TeeTimeOfferSent
        {
            TeeTimeOfferId = Id,
            TeeTimeId = TeeTimeId,
            GolferId = GolferId,
            GroupSize = GroupSize,
        });
    }

    public void MarkAccepted(Guid bookingId)
    {
        if (Status != TeeTimeOfferStatus.Pending)
        {
            throw new OfferNotPendingException();
        }

        Status = TeeTimeOfferStatus.Accepted;

        AddDomainEvent(new TeeTimeOfferAccepted
        {
            TeeTimeOfferId = Id,
            TeeTimeId = TeeTimeId,
            BookingId = bookingId,
            GolferId = GolferId,
            GroupSize = GroupSize,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });
    }

    public void Reject(string reason)
    {
        if (Status != TeeTimeOfferStatus.Pending)
        {
            return; // idempotent — already resolved
        }

        Status = TeeTimeOfferStatus.Rejected;
        RejectionReason = reason;

        AddDomainEvent(new TeeTimeOfferRejected
        {
            TeeTimeOfferId = Id,
            TeeTimeId = TeeTimeId,
            Reason = reason,
        });
    }

    public void Expire()
    {
        if (Status != TeeTimeOfferStatus.Pending)
        {
            return; // idempotent — already resolved
        }

        Status = TeeTimeOfferStatus.Expired;

        AddDomainEvent(new TeeTimeOfferExpired
        {
            TeeTimeOfferId = Id,
            TeeTimeId = TeeTimeId,
        });
    }

    public void MarkStale()
    {
        if (Status != TeeTimeOfferStatus.Pending || IsStale)
        {
            return; // idempotent — already resolved or already stale
        }

        IsStale = true;

        AddDomainEvent(new TeeTimeOfferStale
        {
            TeeTimeOfferId = Id,
            TeeTimeId = TeeTimeId,
        });
    }
}
