namespace Shadowbrook.Domain.WaitlistOfferAggregate;

public interface IWaitlistOfferRepository
{
    Task<WaitlistOffer?> GetByTokenAsync(Guid token);
    Task<WaitlistOffer?> GetByBookingIdAsync(Guid bookingId);
    Task<List<WaitlistOffer>> GetPendingByRequestAsync(Guid teeTimeRequestId);
    void Add(WaitlistOffer offer);
    void AddRange(IEnumerable<WaitlistOffer> offers);
    Task SaveAsync();
}
