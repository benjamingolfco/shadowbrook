namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public interface IGolferWaitlistEntryRepository
{
    Task<GolferWaitlistEntry?> GetByIdAsync(Guid id);
    Task<GolferWaitlistEntry?> GetActiveByWaitlistAndGolferAsync(Guid courseWaitlistId, Guid golferId);
    Task<List<GolferWaitlistEntry>> GetActiveByWaitlistAsync(Guid courseWaitlistId);
    void Add(GolferWaitlistEntry entry);
    Task<List<GolferWaitlistEntry>> FindEligibleEntriesAsync(
        Guid courseId, DateOnly date, TimeOnly teeTime, int maxGroupSize, CancellationToken ct = default);
}
