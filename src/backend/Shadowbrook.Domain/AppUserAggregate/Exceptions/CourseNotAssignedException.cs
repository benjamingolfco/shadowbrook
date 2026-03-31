using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate.Exceptions;

public class CourseNotAssignedException(Guid courseId)
    : DomainException($"User is not assigned to course {courseId}.")
{
    public Guid CourseId { get; } = courseId;
}
