using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public class TeeTimeOpening : Entity
{
    public Guid CourseId { get; private set; }
    public TeeTime TeeTime { get; private set; } = null!;
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
        bool operatorOwned,
        ITimeProvider timeProvider)
    {
        if (slotsAvailable <= 0)
        {
            throw new InvalidSlotsAvailableException();
        }

        var opening = new TeeTimeOpening
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            TeeTime = new TeeTime(date, teeTime),
            SlotsAvailable = slotsAvailable,
            SlotsRemaining = slotsAvailable,
            OperatorOwned = operatorOwned,
            Status = TeeTimeOpeningStatus.Open,
            CreatedAt = timeProvider.GetCurrentTimestamp(),
        };

        opening.AddDomainEvent(new TeeTimeOpeningCreated
        {
            OpeningId = opening.Id,
            CourseId = opening.CourseId,
            Date = opening.TeeTime.Date,
            TeeTime = opening.TeeTime.Time,
            SlotsAvailable = opening.SlotsAvailable,
        });

        return opening;
    }

    public void Claim(Guid bookingId, Guid golferId, int groupSize, ITimeProvider timeProvider)
    {
        if (Status != TeeTimeOpeningStatus.Open)
        {
            throw new OpeningNotAvailableException(Id);
        }

        if (groupSize <= 0)
        {
            throw new InvalidGroupSizeException();
        }

        if (SlotsRemaining < groupSize)
        {
            AddDomainEvent(new TeeTimeOpeningClaimRejected
            {
                OpeningId = Id,
                BookingId = bookingId,
                GolferId = golferId,
            });
            return;
        }

        SlotsRemaining -= groupSize;

        AddDomainEvent(new TeeTimeOpeningClaimed
        {
            OpeningId = Id,
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = CourseId,
            Date = TeeTime.Date,
            TeeTime = TeeTime.Time,
        });

        if (SlotsRemaining == 0)
        {
            Status = TeeTimeOpeningStatus.Filled;
            FilledAt = timeProvider.GetCurrentTimestamp();

            AddDomainEvent(new TeeTimeOpeningFilled
            {
                OpeningId = Id,
            });
        }
    }

    public void Expire(ITimeProvider timeProvider)
    {
        if (Status != TeeTimeOpeningStatus.Open)
        {
            return;
        }

        Status = TeeTimeOpeningStatus.Expired;
        ExpiredAt = timeProvider.GetCurrentTimestamp();

        AddDomainEvent(new TeeTimeOpeningExpired
        {
            OpeningId = Id,
        });
    }
}
