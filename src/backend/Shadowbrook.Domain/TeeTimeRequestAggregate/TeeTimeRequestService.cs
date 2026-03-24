using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public class TeeTimeRequestService(
    ITeeTimeRequestRepository teeTimeRequestRepository,
    IWalkUpWaitlistRepository walkUpWaitlistRepository)
{
    public async Task<TeeTimeRequest> CreateAsync(
        Guid courseId,
        DateOnly date,
        TimeOnly teeTime,
        int golfersNeeded,
        string timeZoneId,
        TimeProvider timeProvider)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var courseLocalNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), tz);
        var today = DateOnly.FromDateTime(courseLocalNow.DateTime);
        var now = TimeOnly.FromDateTime(courseLocalNow.DateTime);
        var gracePeriod = TimeSpan.FromMinutes(5);

        if (date < today || (date == today && teeTime < now.Add(-gracePeriod)))
        {
            throw new TeeTimePastException();
        }

        var waitlist = await walkUpWaitlistRepository.GetOpenByCourseDateAsync(courseId, date);

        if (waitlist is null)
        {
            throw new WaitlistNotOpenForRequestsException(date);
        }

        return await TeeTimeRequest.CreateAsync(courseId, date, teeTime, golfersNeeded, timeZoneId, teeTimeRequestRepository);
    }
}
