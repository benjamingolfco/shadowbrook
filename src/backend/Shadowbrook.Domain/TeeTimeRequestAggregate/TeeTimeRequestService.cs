using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.TeeTimeRequestAggregate;

public class TeeTimeRequestService(
    ITeeTimeRequestRepository teeTimeRequestRepository,
    IWalkUpWaitlistRepository walkUpWaitlistRepository,
    ICourseTimeZoneProvider courseTimeZoneProvider,
    ITimeProvider timeProvider)
{
    public async Task<TeeTimeRequest> CreateAsync(
        Guid courseId,
        DateOnly date,
        TimeOnly teeTime,
        int golfersNeeded)
    {
        var timeZoneId = await courseTimeZoneProvider.GetTimeZoneIdAsync(courseId);
        var today = timeProvider.GetCurrentDateByTimeZone(timeZoneId);
        var now = timeProvider.GetCurrentTimeByTimeZone(timeZoneId);
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
