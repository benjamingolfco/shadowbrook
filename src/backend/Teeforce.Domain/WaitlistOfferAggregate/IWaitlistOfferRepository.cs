using Teeforce.Domain.Common;

namespace Teeforce.Domain.WaitlistOfferAggregate;

public interface IWaitlistOfferRepository : IRepository<WaitlistOffer>
{
    Task<WaitlistOffer?> GetByTokenAsync(Guid token);
    Task<List<WaitlistOffer>> GetPendingByOpeningAsync(Guid openingId);
    void Add(WaitlistOffer offer);
    void AddRange(IEnumerable<WaitlistOffer> offers);
}
