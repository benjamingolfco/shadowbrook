using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Domain.WaitlistServices;

public class WaitlistMatchingService(
    IGolferWaitlistEntryRepository entryRepository,
    ITeeTimeOpeningRepository openingRepository)
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

    public async Task<TeeTimeOpening?> FindOpeningForGolferAsync(
        GolferWaitlistEntry entry, Guid courseId, DateOnly date, CancellationToken ct = default)
    {
        var openings = await openingRepository.FindActiveOpeningsForCourseDateAsync(courseId, date, ct);
        return openings.FirstOrDefault(o => o.IsInGolferWindow(entry));
    }
}
