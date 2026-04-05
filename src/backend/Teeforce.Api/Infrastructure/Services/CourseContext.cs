namespace Teeforce.Api.Infrastructure.Services;

public class CourseContext : ICourseContext
{
    public Guid CourseId { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateOnly Today { get; private set; }
    public TimeOnly Now { get; private set; }

    // Called by CourseExistsMiddleware only
    public void Set(Guid courseId, string timeZoneId, DateOnly today, TimeOnly now)
    {
        CourseId = courseId;
        TimeZoneId = timeZoneId;
        Today = today;
        Now = now;
    }
}
