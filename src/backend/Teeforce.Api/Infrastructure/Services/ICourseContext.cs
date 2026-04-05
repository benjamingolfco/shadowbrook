namespace Teeforce.Api.Infrastructure.Services;

public interface ICourseContext
{
    Guid CourseId { get; }
    string TimeZoneId { get; }
    DateOnly Today { get; }
    TimeOnly Now { get; }
}
