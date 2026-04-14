using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate.Exceptions;

public class ConflictingScheduleException(string scheduleName, string conflictingScheduleName)
    : DomainException($"Schedule '{scheduleName}' conflicts with '{conflictingScheduleName}' — same specificity on overlapping day+time.")
{
    public string ScheduleName { get; } = scheduleName;
    public string ConflictingScheduleName { get; } = conflictingScheduleName;
}
