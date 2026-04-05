using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;

namespace Shadowbrook.Api.Infrastructure.Auth;

public class DevAppUserClaimsProvider(ApplicationDbContext db) : IAppUserClaimsProvider
{
    public async Task<AppUserClaimsData?> GetByIdentityIdAsync(string identityId)
    {
        // In dev mode, the "oid" claim is actually an email address.
        // Look up by email so seeded users don't need fake IdentityIds.
        return await db.AppUsers
            .Where(u => u.Email == identityId)
            .Select(u => new AppUserClaimsData(u.Id, u.OrganizationId, u.Role, u.IsActive))
            .FirstOrDefaultAsync();
    }

    public Task<AppUserClaimsData?> GetByEmailUnlinkedAsync(string email) => Task.FromResult<AppUserClaimsData?>(null);
}
