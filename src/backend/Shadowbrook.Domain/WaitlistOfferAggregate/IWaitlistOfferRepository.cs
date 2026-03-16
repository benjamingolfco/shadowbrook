namespace Shadowbrook.Domain.WaitlistOfferAggregate;

public interface IWaitlistOfferRepository
{
    Task<WaitlistOffer?> GetByTokenAsync(Guid token);
    Task<int> GetAcceptanceCountAsync(Guid teeTimeRequestId);
    Task<List<WaitlistOffer>> GetPendingByRequestAsync(Guid teeTimeRequestId);
    void Add(WaitlistOffer offer);
    void AddRange(IEnumerable<WaitlistOffer> offers);
    Task SaveAsync();
}
