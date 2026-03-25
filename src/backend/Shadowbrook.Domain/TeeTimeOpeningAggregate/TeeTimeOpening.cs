using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public class TeeTimeOpening : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly TeeTime { get; private set; }
    public int SlotsAvailable { get; private set; }
    public int SlotsRemaining { get; private set; }
    public bool OperatorOwned { get; private set; }
    public TeeTimeOpeningStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FilledAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }

    private TeeTimeOpening() { } // EF

    public static TeeTimeOpening Create(
        Guid courseId,
        DateOnly date,
        TimeOnly teeTime,
        int slotsAvailable,
        bool operatorOwned)
    {
        var opening = new TeeTimeOpening
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime,
            SlotsAvailable = slotsAvailable,
            SlotsRemaining = slotsAvailable,
            OperatorOwned = operatorOwned,
            Status = TeeTimeOpeningStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        opening.AddDomainEvent(new TeeTimeOpeningCreated
        {
            OpeningId = opening.Id,
            CourseId = opening.CourseId,
            Date = opening.Date,
            TeeTime = opening.TeeTime,
            SlotsAvailable = opening.SlotsAvailable,
        });

        return opening;
    }

    public void Claim(Guid bookingId, Guid golferId, int groupSize)
    {
        if (this.Status != TeeTimeOpeningStatus.Open)
        {
            throw new OpeningNotAvailableException(this.Id);
        }

        if (this.SlotsRemaining < groupSize)
        {
            this.AddDomainEvent(new TeeTimeOpeningClaimRejected
            {
                OpeningId = this.Id,
                BookingId = bookingId,
                GolferId = golferId,
            });
            return;
        }

        this.SlotsRemaining -= groupSize;

        this.AddDomainEvent(new TeeTimeOpeningClaimed
        {
            OpeningId = this.Id,
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = this.CourseId,
            Date = this.Date,
            TeeTime = this.TeeTime,
        });

        if (this.SlotsRemaining == 0)
        {
            this.Status = TeeTimeOpeningStatus.Filled;
            this.FilledAt = DateTimeOffset.UtcNow;

            this.AddDomainEvent(new TeeTimeOpeningFilled
            {
                OpeningId = this.Id,
            });
        }
    }

    public void Expire()
    {
        if (this.Status != TeeTimeOpeningStatus.Open)
        {
            return;
        }

        this.Status = TeeTimeOpeningStatus.Expired;
        this.ExpiredAt = DateTimeOffset.UtcNow;

        this.AddDomainEvent(new TeeTimeOpeningExpired
        {
            OpeningId = this.Id,
        });
    }
}
