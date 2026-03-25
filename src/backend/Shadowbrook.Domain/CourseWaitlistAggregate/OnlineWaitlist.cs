using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public class OnlineWaitlist : CourseWaitlist
{
    private OnlineWaitlist() { } // EF

    public static OnlineWaitlist Create(Guid courseId, DateOnly date)
    {
        var now = DateTimeOffset.UtcNow;
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
        int groupSize,
        TimeOnly windowStart,
        TimeOnly windowEnd)
    {
        var existing = await entryRepository.GetActiveByWaitlistAndGolferAsync(Id, golfer.Id);
        if (existing is not null)
        {
            throw new GolferAlreadyOnWaitlistException(golfer.Phone);
        }

        var entry = new OnlineGolferWaitlistEntry(Id, golfer.Id, groupSize, windowStart, windowEnd);

        AddDomainEvent(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = Id,
            GolferId = golfer.Id
        });

        return entry;
    }
}
