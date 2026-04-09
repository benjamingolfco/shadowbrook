using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOfferAggregate;

public interface ITeeTimeOfferRepository : IRepository<TeeTimeOffer>
{
    void Add(TeeTimeOffer offer);
    Task<TeeTimeOffer?> GetByTokenAsync(Guid token, CancellationToken ct = default);
    Task<TeeTimeOffer?> GetPendingByTeeTimeAndGolfer(Guid teeTimeId, Guid golferId, CancellationToken ct = default);
}
