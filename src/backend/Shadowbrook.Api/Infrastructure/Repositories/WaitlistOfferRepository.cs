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

    public async Task<List<WaitlistOffer>> GetPendingByOpeningAsync(Guid openingId)
    {
        return await db.WaitlistOffers
            .Where(o => o.OpeningId == openingId && o.Status == OfferStatus.Pending)
            .ToListAsync();
    }

    public async Task<WaitlistOffer?> GetMostRecentPendingWalkUpByGolferAsync(Guid golferId)
    {
        return await db.WaitlistOffers
            .IgnoreQueryFilters()
            .Where(o => o.GolferId == golferId
                && o.IsWalkUp
                && o.Status == OfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public void Add(WaitlistOffer offer) =>
        db.WaitlistOffers.Add(offer);

    public void AddRange(IEnumerable<WaitlistOffer> offers) =>
        db.WaitlistOffers.AddRange(offers);
}
