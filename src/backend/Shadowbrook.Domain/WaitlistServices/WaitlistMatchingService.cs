using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Domain.WaitlistServices;

public class WaitlistMatchingService(IGolferWaitlistEntryRepository entryRepository)
{
    public async Task<List<GolferWaitlistEntry>> FindEligibleEntriesAsync(
        TeeTimeOpening opening, CancellationToken ct = default)
    {
        return await entryRepository.FindEligibleEntriesAsync(
            opening.CourseId,
            opening.TeeTime.Date,
            opening.TeeTime.Time,
            opening.SlotsRemaining,
            ct);
    }
}
