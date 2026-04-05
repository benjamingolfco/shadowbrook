using Teeforce.Domain.Common;

namespace Teeforce.Domain.GolferAggregate;

public interface IGolferRepository : IRepository<Golfer>
{
    Task<Golfer?> GetByPhoneAsync(string phone);
    void Add(Golfer golfer);
}
