using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.WaitlistOfferAggregate;

public class WaitlistOffer : Entity
{
    public Guid Token { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid TeeTimeRequestId { get; private set; }
    public Guid GolferWaitlistEntryId { get; private set; }
    public OfferStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WaitlistOffer() { } // EF

    public static WaitlistOffer Create(
        Guid teeTimeRequestId,
        Guid golferWaitlistEntryId)
    {
        return new WaitlistOffer
        {
            Id = Guid.CreateVersion7(),
            Token = Guid.CreateVersion7(),
            BookingId = Guid.CreateVersion7(),
            TeeTimeRequestId = teeTimeRequestId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            Status = OfferStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Accept(Golfer golfer)
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
            TeeTimeRequestId = TeeTimeRequestId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferId = golfer.Id
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
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            Reason = reason
        });
    }
}
