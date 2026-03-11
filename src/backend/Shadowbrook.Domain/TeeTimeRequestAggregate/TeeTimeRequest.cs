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
