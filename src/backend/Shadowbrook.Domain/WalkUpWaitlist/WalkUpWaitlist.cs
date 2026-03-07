using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Domain.WalkUpWaitlist;

public class WalkUpWaitlist : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public string ShortCode { get; private set; } = string.Empty;
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<TeeTimeRequest> teeTimeRequests = [];
    public IReadOnlyCollection<TeeTimeRequest> TeeTimeRequests => this.teeTimeRequests.AsReadOnly();

    private WalkUpWaitlist() { } // EF

    public static async Task<WalkUpWaitlist> OpenAsync(
        Guid courseId, DateOnly date, IShortCodeGenerator shortCodeGenerator,
        IWalkUpWaitlistRepository repository)
    {
        var existing = await repository.GetByCourseDateAsync(courseId, date);
        if (existing is not null)
        {
            throw new WaitlistAlreadyExistsException(existing.Status);
        }

        var shortCode = await shortCodeGenerator.GenerateAsync(date);
        var now = DateTimeOffset.UtcNow;
        return new WalkUpWaitlist
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            ShortCode = shortCode,
            Status = WaitlistStatus.Open,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Close()
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var now = DateTimeOffset.UtcNow;
        Status = WaitlistStatus.Closed;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public TeeTimeRequest AddTeeTimeRequest(TimeOnly teeTime, int golfersNeeded)
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var duplicate = this.teeTimeRequests.Any(r =>
            r.TeeTime == teeTime && r.Status == RequestStatus.Pending);
        if (duplicate)
        {
            throw new DuplicateTeeTimeRequestException(teeTime);
        }

        var request = new TeeTimeRequest(Id, teeTime, golfersNeeded);
        this.teeTimeRequests.Add(request);

        AddDomainEvent(new TeeTimeRequestAdded
        {
            WaitlistId = Id,
            TeeTimeRequestId = request.Id,
            CourseId = CourseId,
            Date = Date,
            TeeTime = teeTime,
            GolfersNeeded = golfersNeeded
        });

        UpdatedAt = DateTimeOffset.UtcNow;
        return request;
    }
}
