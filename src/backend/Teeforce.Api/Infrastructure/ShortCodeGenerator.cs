using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.CourseWaitlistAggregate;

namespace Teeforce.Api.Infrastructure;

public class ShortCodeGenerator(ApplicationDbContext db) : IShortCodeGenerator
{
    public async Task<string> GenerateAsync(DateOnly date)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = Random.Shared.Next(0, 10000).ToString("D4");
            var taken = await db.CourseWaitlists
                .OfType<WalkUpWaitlist>()
                .AnyAsync(w => w.ShortCode == candidate && w.Date == date);
            if (!taken)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique short code.");
    }
}
