using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

namespace Shadowbrook.Domain.WaitlistServices;

public class WaitlistMatchingService(IGolferWaitlistEntryRepository entryRepository)
{
    public async Task<List<GolferWaitlistEntry>> FindEligibleEntriesAsync(
        TeeTimeOpening opening, CancellationToken ct = default)
    {
        if (opening.Status != TeeTimeOpeningStatus.Open)
        {
            throw new OpeningNotAvailableException(opening.Id);
        }

        return await entryRepository.FindEligibleEntriesAsync(
            opening.CourseId,
            opening.TeeTime.Date,
            opening.TeeTime.Time,
            opening.SlotsRemaining,
            ct);
    }
}
