using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public abstract class CourseWaitlist : Entity
{
    public Guid CourseId { get; protected set; }
    public DateOnly Date { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }

    // ReSharper disable once EmptyConstructor
    protected CourseWaitlist() { } // EF

    public abstract Task<GolferWaitlistEntry> Join(
        Golfer golfer, IGolferWaitlistEntryRepository entryRepository, int groupSize = 1);
}
