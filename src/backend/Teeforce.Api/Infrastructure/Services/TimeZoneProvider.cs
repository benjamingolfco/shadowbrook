using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public class TimeZoneProvider(TimeProvider timeProvider) : ITimeProvider
{
    public DateOnly GetCurrentDate() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    public TimeOnly GetCurrentTime() => TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    public DateOnly GetCurrentDateByTimeZone(string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var courseLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz);
        return DateOnly.FromDateTime(courseLocal.DateTime);
    }

    public TimeOnly GetCurrentTimeByTimeZone(string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var courseLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz);
        return TimeOnly.FromDateTime(courseLocal.DateTime);
    }

    public DateTimeOffset GetCurrentTimestamp() => timeProvider.GetUtcNow();

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
