using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Api.Models;

public class WaitlistOffer : Entity
{
    public WaitlistOffer() { } // EF + public init

    public required Guid TeeTimeRequestId { get; set; }
    public required Guid GolferWaitlistEntryId { get; set; }
    public required string GolferPhone { get; set; }
    public required string CourseName { get; set; }
    public required TimeOnly TeeTime { get; set; }
    public required DateOnly OfferDate { get; set; }
    public OfferStatus Status { get; set; } = OfferStatus.Pending;
    public int ResponseWindowMinutes { get; set; } = 5;
    public DateTimeOffset OfferedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public void Accept(DateTimeOffset respondedAt)
    {
        if (Status != OfferStatus.Pending)
        {
            throw new WaitlistOfferNotPendingException(Status);
        }

        Status = OfferStatus.Accepted;
        RespondedAt = respondedAt;

        AddDomainEvent(new WaitlistOfferAccepted
        {
            WaitlistOfferId = Id,
            TeeTimeRequestId = TeeTimeRequestId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferPhone = GolferPhone,
            CourseName = CourseName,
            TeeTime = TeeTime,
            OfferDate = OfferDate
        });
    }

    public void Decline(DateTimeOffset respondedAt)
    {
        if (Status != OfferStatus.Pending)
        {
            throw new WaitlistOfferNotPendingException(Status);
        }

        Status = OfferStatus.Declined;
        RespondedAt = respondedAt;

        AddDomainEvent(new WaitlistOfferDeclined
        {
            WaitlistOfferId = Id,
            TeeTimeRequestId = TeeTimeRequestId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            GolferPhone = GolferPhone
        });
    }
}
