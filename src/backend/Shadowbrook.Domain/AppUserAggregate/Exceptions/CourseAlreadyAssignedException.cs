using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate.Exceptions;

public class CourseAlreadyAssignedException(Guid courseId)
    : DomainException($"User is already assigned to course {courseId}.")
{
    public Guid CourseId { get; } = courseId;
}
