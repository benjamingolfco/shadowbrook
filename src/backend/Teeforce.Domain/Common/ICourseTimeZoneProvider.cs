namespace Teeforce.Domain.Common;

public interface ICourseTimeZoneProvider
{
    Task<string> GetTimeZoneIdAsync(Guid courseId);
}
