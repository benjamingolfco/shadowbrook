using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate.Events;
using Teeforce.Domain.CourseWaitlistAggregate.Exceptions;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;

namespace Teeforce.Domain.CourseWaitlistAggregate;

public class OnlineWaitlist : CourseWaitlist
{
    private OnlineWaitlist() { } // EF

    public static OnlineWaitlist Create(Guid courseId, DateOnly date, ITimeProvider timeProvider)
    {
        var now = timeProvider.GetCurrentTimestamp();
        return new OnlineWaitlist
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            CreatedAt = now
        };
    }

    public async Task<OnlineGolferWaitlistEntry> Join(
        Golfer golfer,
        IGolferWaitlistEntryRepository entryRepository,
        ITimeProvider timeProvider,
        int groupSize,
        DateTime windowStart,
        DateTime windowEnd)
    {
        var existing = await entryRepository.GetActiveByWaitlistAndGolferAsync(Id, golfer.Id);
        if (existing is not null)
        {
            throw new GolferAlreadyOnWaitlistException(golfer.Phone);
        }

        var now = timeProvider.GetCurrentTimestamp();
        var entry = new OnlineGolferWaitlistEntry(Id, golfer.Id, groupSize, windowStart, windowEnd, now);

        AddDomainEvent(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = Id,
            GolferId = golfer.Id
        });

        return entry;
    }
}
