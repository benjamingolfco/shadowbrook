namespace Teeforce.Domain.Common;

public interface ITimeProvider
{
    DateOnly GetCurrentDate();
    TimeOnly GetCurrentTime();
    DateOnly GetCurrentDateByTimeZone(string timeZoneId);
    TimeOnly GetCurrentTimeByTimeZone(string timeZoneId);
    DateTimeOffset GetCurrentTimestamp();
}
