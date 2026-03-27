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
    public DateTimeOffset? CancelledAt { get; private set; }

    private readonly List<ClaimedSlot> claimedSlots = [];
    public IReadOnlyList<ClaimedSlot> ClaimedSlots => this.claimedSlots.AsReadOnly();

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

    public ClaimResult TryClaim(Guid bookingId, Guid golferId, int groupSize, ITimeProvider timeProvider)
    {
        if (groupSize <= 0)
        {
            throw new InvalidGroupSizeException();
        }

        if (Status != TeeTimeOpeningStatus.Open)
        {
            AddDomainEvent(new TeeTimeOpeningSlotsClaimRejected
            {
                OpeningId = Id,
                BookingId = bookingId,
                GolferId = golferId,
            });
            return ClaimResult.Rejected("Opening is not available");
        }

        if (SlotsRemaining < groupSize)
        {
            AddDomainEvent(new TeeTimeOpeningSlotsClaimRejected
            {
                OpeningId = Id,
                BookingId = bookingId,
                GolferId = golferId,
            });
            return ClaimResult.Rejected("Insufficient slots remaining");
        }

        SlotsRemaining -= groupSize;
        this.claimedSlots.Add(new ClaimedSlot(bookingId, golferId, groupSize, timeProvider.GetCurrentTimestamp()));

        AddDomainEvent(new TeeTimeOpeningSlotsClaimed
        {
            OpeningId = Id,
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = CourseId,
            Date = TeeTime.Date,
            TeeTime = TeeTime.Time,
            GroupSize = groupSize,
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

        return ClaimResult.Claimed();
    }

    public void Cancel(ITimeProvider timeProvider)
    {
        if (Status != TeeTimeOpeningStatus.Open)
        {
            return;
        }

        Status = TeeTimeOpeningStatus.Cancelled;
        CancelledAt = timeProvider.GetCurrentTimestamp();

        AddDomainEvent(new TeeTimeOpeningCancelled
        {
            OpeningId = Id,
            CourseId = CourseId,
            Date = TeeTime.Date,
            TeeTime = TeeTime.Time,
        });
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
