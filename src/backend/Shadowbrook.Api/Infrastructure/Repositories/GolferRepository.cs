using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class GolferRepository(ApplicationDbContext db) : IGolferRepository
{
    public async Task<Golfer?> GetByIdAsync(Guid id)
    {
        return await db.Golfers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Golfer?> GetByPhoneAsync(string phone)
    {
        return await db.Golfers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Phone == phone);
    }

    public void Add(Golfer golfer) =>
        db.Golfers.Add(golfer);
}
