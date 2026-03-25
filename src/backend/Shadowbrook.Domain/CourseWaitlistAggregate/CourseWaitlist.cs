using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public abstract class CourseWaitlist : Entity
{
    public Guid CourseId { get; protected set; }
    public DateOnly Date { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }

    protected CourseWaitlist() { } // EF
}
