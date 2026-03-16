using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.WaitlistOfferAggregate;

public class WaitlistOffer : Entity
{
    public Guid Token { get; private set; }
    public Guid TeeTimeRequestId { get; private set; }
    public Guid GolferWaitlistEntryId { get; private set; }
    public Guid CourseId { get; private set; }
    public string CourseName { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public TimeOnly TeeTime { get; private set; }
    public int GolfersNeeded { get; private set; }
    public string GolferName { get; private set; } = string.Empty;
    public string GolferPhone { get; private set; } = string.Empty;
    public OfferStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WaitlistOffer() { } // EF

    public static WaitlistOffer Create(
        Guid teeTimeRequestId,
        Guid golferWaitlistEntryId,
        Guid courseId,
        string courseName,
        DateOnly date,
        TimeOnly teeTime,
        int golfersNeeded,
        string golferName,
        string golferPhone,
        DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new WaitlistOffer
        {
            Id = Guid.CreateVersion7(),
            Token = Guid.CreateVersion7(),
            TeeTimeRequestId = teeTimeRequestId,
            GolferWaitlistEntryId = golferWaitlistEntryId,
            CourseId = courseId,
            CourseName = courseName,
            Date = date,
            TeeTime = teeTime,
            GolfersNeeded = golfersNeeded,
            GolferName = golferName,
            GolferPhone = golferPhone,
            Status = OfferStatus.Pending,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
    }

    public void Accept(int currentAcceptanceCount)
    {
        if (Status != OfferStatus.Pending)
        {
            throw new OfferNotPendingException();
        }

        if (ExpiresAt < DateTimeOffset.UtcNow)
        {
            Status = OfferStatus.Expired;
            throw new OfferExpiredException();
        }

        if (currentAcceptanceCount >= GolfersNeeded)
        {
            throw new OfferSlotsFilledException();
        }

        Status = OfferStatus.Accepted;

        AddDomainEvent(new WaitlistOfferAccepted
        {
            WaitlistOfferId = Id,
            TeeTimeRequestId = TeeTimeRequestId,
            GolferWaitlistEntryId = GolferWaitlistEntryId,
            CourseId = CourseId,
            CourseName = CourseName,
            Date = Date,
            TeeTime = TeeTime,
            GolferName = GolferName,
            GolferPhone = GolferPhone,
            GolfersNeeded = GolfersNeeded,
            AcceptanceCount = currentAcceptanceCount + 1
        });
    }

    public void Expire()
    {
        if (Status == OfferStatus.Pending)
        {
            Status = OfferStatus.Expired;
        }
    }

    public bool CheckExpiration()
    {
        if (Status == OfferStatus.Pending && ExpiresAt < DateTimeOffset.UtcNow)
        {
            Status = OfferStatus.Expired;
            return true;
        }
        return false;
    }
}
