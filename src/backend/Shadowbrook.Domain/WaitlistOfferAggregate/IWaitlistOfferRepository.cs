namespace Shadowbrook.Domain.WaitlistOfferAggregate;

public interface IWaitlistOfferRepository
{
    Task<WaitlistOffer?> GetByIdAsync(Guid id);
    Task<WaitlistOffer?> GetByTokenAsync(Guid token);
    Task<List<WaitlistOffer>> GetPendingByOpeningAsync(Guid openingId);
    void Add(WaitlistOffer offer);
    void AddRange(IEnumerable<WaitlistOffer> offers);
}
