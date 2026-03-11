namespace Shadowbrook.Domain.GolferAggregate;

public interface IGolferRepository
{
    Task<Golfer?> GetByIdAsync(Guid id);
    Task<Golfer?> GetByPhoneAsync(string phone);
    void Add(Golfer golfer);
    Task SaveAsync();
}
