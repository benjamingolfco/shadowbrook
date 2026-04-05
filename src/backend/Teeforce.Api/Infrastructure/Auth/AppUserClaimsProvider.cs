using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;

namespace Teeforce.Api.Infrastructure.Auth;

public class AppUserClaimsProvider(ApplicationDbContext db) : IAppUserClaimsProvider
{
    public async Task<AppUserClaimsData?> GetByIdentityIdAsync(string identityId)
    {
        return await db.AppUsers
            .Where(u => u.IdentityId == identityId)
            .Select(u => new AppUserClaimsData(u.Id, u.OrganizationId, u.Role, u.IsActive))
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserClaimsData?> GetByEmailUnlinkedAsync(string email)
    {
        return await db.AppUsers
            .Where(u => u.Email == email && u.IdentityId == null)
            .Select(u => new AppUserClaimsData(u.Id, u.OrganizationId, u.Role, u.IsActive))
            .FirstOrDefaultAsync();
    }
}
