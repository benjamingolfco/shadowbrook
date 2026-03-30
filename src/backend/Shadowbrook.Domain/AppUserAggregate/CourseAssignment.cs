using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate;

public class CourseAssignment : Entity
{
    public Guid AppUserId { get; private set; }
    public Guid CourseId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    private CourseAssignment() { } // EF

    internal static CourseAssignment Create(Guid appUserId, Guid courseId)
    {
        return new CourseAssignment
        {
            Id = Guid.CreateVersion7(),
            AppUserId = appUserId,
            CourseId = courseId,
            AssignedAt = DateTimeOffset.UtcNow,
        };
    }
}
