using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate.Exceptions;

namespace Teeforce.Domain.TeeTimeAggregate;

public class TeeTime : Entity
{
    public Guid TeeSheetId { get; private set; }
    public Guid TeeSheetIntervalId { get; private set; }
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }
    public int Remaining { get; private set; }
    public TeeTimeStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeTimeClaim> claims = [];
    public IReadOnlyList<TeeTimeClaim> Claims => this.claims.AsReadOnly();

    private TeeTime() { } // EF

    public static TeeTime Claim(
        TeeSheetInterval interval,
        Guid courseId,
        DateOnly date,
        BookingAuthorization auth,
        Guid bookingId,
        Guid golferId,
        int groupSize,
        ITimeProvider timeProvider)
    {
        if (interval.Capacity <= 0)
        {
            throw new InvalidTeeTimeCapacityException();
        }
        if (auth.SheetId != interval.TeeSheetId)
        {
            throw new BookingAuthorizationMismatchException(Guid.Empty, auth.SheetId, interval.TeeSheetId);
        }
        if (groupSize <= 0)
        {
            throw new InvalidGroupSizeException();
        }
        if (groupSize > interval.Capacity)
        {
            throw new InsufficientCapacityException(Guid.Empty, groupSize, interval.Capacity);
        }

        var now = timeProvider.GetCurrentTimestamp();
        var teeTime = new TeeTime
        {
            Id = Guid.CreateVersion7(),
            TeeSheetId = interval.TeeSheetId,
            TeeSheetIntervalId = interval.Id,
            CourseId = courseId,
            Date = date,
            Time = interval.Time,
            Capacity = interval.Capacity,
            Remaining = interval.Capacity,
            Status = TeeTimeStatus.Open,
            CreatedAt = now,
        };

        teeTime.ApplyClaim(bookingId, golferId, groupSize, interval.Price, now);
        return teeTime;
    }

    public void Claim(
        BookingAuthorization auth,
        Guid bookingId,
        Guid golferId,
        int groupSize,
        decimal? price,
        ITimeProvider timeProvider)
    {
        if (auth.SheetId != TeeSheetId)
        {
            throw new BookingAuthorizationMismatchException(Id, auth.SheetId, TeeSheetId);
        }
        if (Status == TeeTimeStatus.Blocked)
        {
            throw new TeeTimeBlockedException(Id);
        }
        if (Status == TeeTimeStatus.Filled || Remaining == 0)
        {
            throw new TeeTimeFilledException(Id);
        }
        if (groupSize <= 0)
        {
            throw new InvalidGroupSizeException();
        }
        if (groupSize > Remaining)
        {
            throw new InsufficientCapacityException(Id, groupSize, Remaining);
        }

        ApplyClaim(bookingId, golferId, groupSize, price, timeProvider.GetCurrentTimestamp());
    }

    private void ApplyClaim(Guid bookingId, Guid golferId, int groupSize, decimal? price, DateTimeOffset now)
    {
        this.claims.Add(new TeeTimeClaim(Id, bookingId, golferId, groupSize, price, now));
        Remaining -= groupSize;

        AddDomainEvent(new TeeTimeClaimed
        {
            TeeTimeId = Id,
            BookingId = bookingId,
            GolferId = golferId,
            GroupSize = groupSize,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
            Price = price,
        });

        AddDomainEvent(new TeeTimeAvailabilityChanged
        {
            TeeTimeId = Id,
            Remaining = Remaining,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });

        if (Remaining == 0)
        {
            Status = TeeTimeStatus.Filled;
            AddDomainEvent(new TeeTimeFilled
            {
                TeeTimeId = Id,
                CourseId = CourseId,
                Date = Date,
                Time = Time,
            });
        }
    }

    public void ReleaseClaim(Guid bookingId, ITimeProvider timeProvider)
    {
        var claim = this.claims.FirstOrDefault(c => c.BookingId == bookingId);
        if (claim is null)
        {
            return; // idempotent — release event may arrive twice
        }

        this.claims.Remove(claim);
        Remaining += claim.GroupSize;
        var wasFilled = Status == TeeTimeStatus.Filled;

        AddDomainEvent(new TeeTimeClaimReleased
        {
            TeeTimeId = Id,
            BookingId = claim.BookingId,
            GolferId = claim.GolferId,
            GroupSize = claim.GroupSize,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });

        AddDomainEvent(new TeeTimeAvailabilityChanged
        {
            TeeTimeId = Id,
            Remaining = Remaining,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });

        if (wasFilled)
        {
            Status = TeeTimeStatus.Open;
            AddDomainEvent(new TeeTimeReopened
            {
                TeeTimeId = Id,
                CourseId = CourseId,
                Date = Date,
                Time = Time,
            });
        }
    }

    public static TeeTime Block(
        TeeSheetInterval interval,
        Guid courseId,
        DateOnly date,
        string reason,
        ITimeProvider timeProvider)
    {
        if (interval.Capacity <= 0)
        {
            throw new InvalidTeeTimeCapacityException();
        }

        var teeTime = new TeeTime
        {
            Id = Guid.CreateVersion7(),
            TeeSheetId = interval.TeeSheetId,
            TeeSheetIntervalId = interval.Id,
            CourseId = courseId,
            Date = date,
            Time = interval.Time,
            Capacity = interval.Capacity,
            Remaining = interval.Capacity,
            Status = TeeTimeStatus.Blocked,
            CreatedAt = timeProvider.GetCurrentTimestamp(),
        };

        teeTime.AddDomainEvent(new TeeTimeBlocked
        {
            TeeTimeId = teeTime.Id,
            CourseId = courseId,
            Date = date,
            Time = interval.Time,
            Reason = reason,
        });

        return teeTime;
    }

    public void Block(string reason, ITimeProvider timeProvider)
    {
        if (Status == TeeTimeStatus.Blocked)
        {
            return;
        }
        if (this.claims.Count > 0)
        {
            throw new TeeTimeHasClaimsException(Id, this.claims.Count);
        }

        Status = TeeTimeStatus.Blocked;
        AddDomainEvent(new TeeTimeBlocked
        {
            TeeTimeId = Id,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
            Reason = reason,
        });
    }

    public void Unblock(ITimeProvider timeProvider)
    {
        if (Status != TeeTimeStatus.Blocked)
        {
            return;
        }
        Status = TeeTimeStatus.Open;
        AddDomainEvent(new TeeTimeUnblocked
        {
            TeeTimeId = Id,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });
    }
}
