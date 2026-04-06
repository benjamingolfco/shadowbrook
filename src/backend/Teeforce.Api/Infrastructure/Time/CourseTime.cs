namespace Teeforce.Api.Infrastructure.Time;

public static class CourseTime
{
    public static DateOnly Today(TimeProvider timeProvider, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var utcNow = timeProvider.GetUtcNow();
        var courseLocal = TimeZoneInfo.ConvertTime(utcNow, tz);
        return DateOnly.FromDateTime(courseLocal.DateTime);
    }

    public static TimeOnly Now(TimeProvider timeProvider, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var utcNow = timeProvider.GetUtcNow();
        var courseLocal = TimeZoneInfo.ConvertTime(utcNow, tz);
        return TimeOnly.FromDateTime(courseLocal.DateTime);
    }

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localDateTime = date.ToDateTime(time);
        var offset = tz.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }
}
