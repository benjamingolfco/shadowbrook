using Teeforce.Domain.GolferWaitlistEntryAggregate;

namespace Teeforce.Domain.WaitlistServices;

public interface ITeeTimeWaitlistMatcher
{
    Task<List<GolferWaitlistEntry>> FindEligibleEntries(
        Guid teeTimeId,
        Guid courseId,
        DateOnly date,
        TimeOnly time,
        int availableSlots,
        CancellationToken ct);
}
