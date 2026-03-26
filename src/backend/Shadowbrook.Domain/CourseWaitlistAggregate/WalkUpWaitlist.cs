using Shadowbrook.Domain.Common;
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
        ICourseWaitlistRepository repository, ITimeProvider timeProvider)
    {
        var existing = await repository.GetByCourseDateAsync(courseId, date);
        if (existing is not null)
        {
            throw new WaitlistAlreadyExistsException(existing.Status);
        }

        var shortCode = await shortCodeGenerator.GenerateAsync(date);
        var now = timeProvider.GetCurrentTimestamp();
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
        Golfer golfer, IGolferWaitlistEntryRepository entryRepository, ITimeProvider timeProvider, int groupSize = 1)
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

        // Note: TimeOnly wraps at midnight. If a golfer joins near midnight UTC,
        // WindowEnd could be earlier than WindowStart (e.g., 23:45 -> 00:15).
        // The repository query must handle this wrap-around case.
        var windowStart = timeProvider.GetCurrentTime();
        var windowEnd = windowStart.Add(TimeSpan.FromMinutes(30));
        var now = timeProvider.GetCurrentTimestamp();

        var entry = new WalkUpGolferWaitlistEntry(Id, golfer.Id, groupSize, windowStart, windowEnd, now);

        AddDomainEvent(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = Id,
            GolferId = golfer.Id
        });

        return entry;
    }

    public void Close(ITimeProvider timeProvider)
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        Status = WaitlistStatus.Closed;
        ClosedAt = timeProvider.GetCurrentTimestamp();

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
