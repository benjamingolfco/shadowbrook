using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistServices;

namespace Teeforce.Api.Infrastructure.Services;

public class EmptyTeeTimeWaitlistMatcher : ITeeTimeWaitlistMatcher
{
    public Task<List<GolferWaitlistEntry>> FindEligibleEntries(
        Guid teeTimeId,
        Guid courseId,
        DateOnly date,
        TimeOnly time,
        int availableSlots,
        CancellationToken ct) => Task.FromResult(new List<GolferWaitlistEntry>());
}
