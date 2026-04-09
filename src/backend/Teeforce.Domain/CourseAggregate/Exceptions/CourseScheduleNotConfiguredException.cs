using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseAggregate.Exceptions;

public class CourseScheduleNotConfiguredException(Guid courseId)
    : DomainException($"Course {courseId} has no tee time schedule configured.")
{
}
