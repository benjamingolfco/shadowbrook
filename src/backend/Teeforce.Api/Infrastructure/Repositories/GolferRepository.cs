using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.GolferAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

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
