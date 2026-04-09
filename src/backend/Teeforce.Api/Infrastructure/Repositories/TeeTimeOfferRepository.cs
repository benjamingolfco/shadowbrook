using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeTimeOfferAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class TeeTimeOfferRepository(ApplicationDbContext db) : ITeeTimeOfferRepository
{
    public async Task<TeeTimeOffer?> GetByIdAsync(Guid id) =>
        await db.TeeTimeOffers.FirstOrDefaultAsync(o => o.Id == id);

    public async Task<TeeTimeOffer?> GetByTokenAsync(Guid token, CancellationToken ct = default) =>
        await db.TeeTimeOffers.FirstOrDefaultAsync(o => o.Token == token, ct);

    public async Task<TeeTimeOffer?> GetPendingByTeeTimeAndGolfer(
        Guid teeTimeId, Guid golferId, CancellationToken ct = default) =>
        await db.TeeTimeOffers.FirstOrDefaultAsync(
            o => o.TeeTimeId == teeTimeId && o.GolferId == golferId && o.Status == TeeTimeOfferStatus.Pending, ct);

    public void Add(TeeTimeOffer offer) => db.TeeTimeOffers.Add(offer);
}
