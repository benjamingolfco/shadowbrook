using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class AppUserRepository(ApplicationDbContext db) : IAppUserRepository
{
    public async Task<AppUser?> GetByIdAsync(Guid id) =>
        await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id);

    public void Add(AppUser appUser) => db.AppUsers.Add(appUser);
}
