using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class WaitlistOfferRepository(ApplicationDbContext db) : IWaitlistOfferRepository
{
    public async Task<WaitlistOffer?> GetByIdAsync(Guid id) =>
        await db.WaitlistOffers.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == id);

    public async Task<WaitlistOffer?> GetByTokenAsync(Guid token)
    {
        return await db.WaitlistOffers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Token == token);
    }

    public async Task<WaitlistOffer?> GetByBookingIdAsync(Guid bookingId)
    {
        return await db.WaitlistOffers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.BookingId == bookingId);
    }

    public async Task<List<WaitlistOffer>> GetPendingByRequestAsync(Guid teeTimeRequestId)
    {
        return await db.WaitlistOffers
            .Where(o => o.TeeTimeRequestId == teeTimeRequestId && o.Status == OfferStatus.Pending)
            .ToListAsync();
    }

    public void Add(WaitlistOffer offer) =>
        db.WaitlistOffers.Add(offer);

    public void AddRange(IEnumerable<WaitlistOffer> offers) =>
        db.WaitlistOffers.AddRange(offers);
}
