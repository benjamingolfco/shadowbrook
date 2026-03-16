using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public class TeeTimeRequest : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly TeeTime { get; private set; }
    public int GolfersNeeded { get; private set; }
    public TeeTimeRequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<TeeTimeSlotFill> slotFills = [];
    public IReadOnlyCollection<TeeTimeSlotFill> SlotFills => this.slotFills.AsReadOnly();

    public int RemainingSlots => GolfersNeeded - this.slotFills.Sum(f => f.GroupSize);

    private TeeTimeRequest() { } // EF

    private TeeTimeRequest(Guid courseId, DateOnly date, TimeOnly teeTime, int golfersNeeded)
    {
        var now = DateTimeOffset.UtcNow;
        Id = Guid.CreateVersion7();
        CourseId = courseId;
        Date = date;
        TeeTime = teeTime;
        GolfersNeeded = golfersNeeded;
        Status = TeeTimeRequestStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;

        AddDomainEvent(new TeeTimeRequestAdded
        {
            TeeTimeRequestId = Id,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime,
            GolfersNeeded = golfersNeeded
        });
    }

    public void MarkFulfilled()
    {
        Status = TeeTimeRequestStatus.Fulfilled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal FillResult Fill(Guid golferId, int groupSize, Guid bookingId)
    {
        if (Status == TeeTimeRequestStatus.Fulfilled)
        {
            return new FillResult(false, "This tee time has already been filled.");
        }

        if (groupSize > RemainingSlots)
        {
            return new FillResult(false, "Your group is too large for the remaining slots.");
        }

        var fill = new TeeTimeSlotFill(Id, golferId, bookingId, groupSize);
        this.slotFills.Add(fill);
        UpdatedAt = DateTimeOffset.UtcNow;

        if (RemainingSlots <= 0)
        {
            Status = TeeTimeRequestStatus.Fulfilled;
            AddDomainEvent(new TeeTimeRequestFulfilled
            {
                TeeTimeRequestId = Id
            });
        }

        return new FillResult(true);
    }

    internal void Unfill(Guid bookingId)
    {
        var fill = this.slotFills.FirstOrDefault(f => f.BookingId == bookingId);
        if (fill is not null)
        {
            this.slotFills.Remove(fill);
            if (Status == TeeTimeRequestStatus.Fulfilled)
            {
                Status = TeeTimeRequestStatus.Pending;
            }
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public static async Task<TeeTimeRequest> CreateAsync(
        Guid courseId,
        DateOnly date,
        TimeOnly teeTime,
        int golfersNeeded,
        ITeeTimeRequestRepository repository)
    {
        var exists = await repository.ExistsAsync(courseId, date, teeTime);
        if (exists)
        {
            throw new DuplicateTeeTimeRequestException(teeTime);
        }

        return new TeeTimeRequest(courseId, date, teeTime, golfersNeeded);
    }
}
