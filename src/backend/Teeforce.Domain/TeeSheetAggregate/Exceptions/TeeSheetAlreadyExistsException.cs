using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Exceptions;

public class TeeSheetAlreadyExistsException(Guid courseId, DateOnly date)
    : DomainException($"A tee sheet for course {courseId} on {date:yyyy-MM-dd} already exists.")
{
    public Guid CourseId { get; } = courseId;
    public DateOnly Date { get; } = date;
}
