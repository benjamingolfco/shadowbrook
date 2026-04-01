using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.GolferAggregate;

public interface IGolferRepository : IRepository<Golfer>
{
    Task<Golfer?> GetByPhoneAsync(string phone);
    void Add(Golfer golfer);
}
