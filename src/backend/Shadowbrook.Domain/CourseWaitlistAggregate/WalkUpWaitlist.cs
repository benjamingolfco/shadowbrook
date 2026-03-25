using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public class WalkUpWaitlist : CourseWaitlist
{
    public string ShortCode { get; private set; } = string.Empty;
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    private WalkUpWaitlist() { } // EF

    public static async Task<WalkUpWaitlist> OpenAsync(
        Guid courseId, DateOnly date, IShortCodeGenerator shortCodeGenerator,
        ICourseWaitlistRepository repository)
    {
        var existing = await repository.GetByCourseDateAsync(courseId, date);
        if (existing is not null)
        {
            throw new WaitlistAlreadyExistsException(existing.Status);
        }

        var shortCode = await shortCodeGenerator.GenerateAsync(date);
        var now = DateTimeOffset.UtcNow;
        var waitlist = new WalkUpWaitlist
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            ShortCode = shortCode,
            Status = WaitlistStatus.Open,
            OpenedAt = now,
            CreatedAt = now
        };

        waitlist.AddDomainEvent(new WalkUpWaitlistOpened { CourseWaitlistId = waitlist.Id });

        return waitlist;
    }

    public async Task<WalkUpGolferWaitlistEntry> Join(
        Golfer golfer, IGolferWaitlistEntryRepository entryRepository, int groupSize = 1)
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var existing = await entryRepository.GetActiveByWaitlistAndGolferAsync(Id, golfer.Id);
        if (existing is not null)
        {
            throw new GolferAlreadyOnWaitlistException(golfer.Phone);
        }

        var windowStart = TimeOnly.FromDateTime(DateTime.UtcNow);
        var windowEnd = windowStart.Add(TimeSpan.FromMinutes(30));

        var entry = new WalkUpGolferWaitlistEntry(Id, golfer.Id, groupSize, windowStart, windowEnd);

        AddDomainEvent(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = Id,
            GolferId = golfer.Id
        });

        return entry;
    }

    public void Close()
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        Status = WaitlistStatus.Closed;
        ClosedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new WalkUpWaitlistClosed { CourseWaitlistId = Id });
    }

    public void Reopen()
    {
        if (Status != WaitlistStatus.Closed)
        {
            throw new WaitlistNotClosedException();
        }

        Status = WaitlistStatus.Open;
        ClosedAt = null;

        AddDomainEvent(new WalkUpWaitlistReopened { CourseWaitlistId = Id });
    }
}
