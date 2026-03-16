using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class WaitlistOfferRepository(ApplicationDbContext db) : IWaitlistOfferRepository
{
    public async Task<WaitlistOffer?> GetByTokenAsync(Guid token)
    {
        return await db.WaitlistOffers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Token == token);
    }

    public async Task<int> GetAcceptanceCountAsync(Guid teeTimeRequestId)
    {
        return await db.WaitlistRequestAcceptances
            .CountAsync(a => a.WaitlistRequestId == teeTimeRequestId);
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

    public async Task SaveAsync() =>
        await db.SaveChangesAsync();
}
