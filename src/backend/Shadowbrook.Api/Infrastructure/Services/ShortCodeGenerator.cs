using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.Services;

public class ShortCodeGenerator(ApplicationDbContext db) : IShortCodeGenerator
{
    public async Task<string> GenerateAsync(DateOnly date)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = Random.Shared.Next(0, 10000).ToString("D4");
            var taken = await db.WalkUpWaitlists
                .AnyAsync(w => w.ShortCode == candidate && w.Date == date);
            if (!taken)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique short code.");
    }
}
