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
        int golfersNeeded)
    {
        var waitlist = await walkUpWaitlistRepository.GetOpenByCourseDateAsync(courseId, date);

        if (waitlist is null)
        {
            throw new WaitlistNotOpenForRequestsException(date);
        }

        return await TeeTimeRequest.CreateAsync(courseId, date, teeTime, golfersNeeded, teeTimeRequestRepository);
    }
}
