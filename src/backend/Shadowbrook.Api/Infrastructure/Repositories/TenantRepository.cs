using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.TenantAggregate;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class TenantRepository(ApplicationDbContext db) : ITenantRepository
{
    public async Task<Tenant?> GetByIdAsync(Guid id) =>
        await db.Tenants.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<List<Tenant>> GetAllAsync() =>
        await db.Tenants.ToListAsync();

    public async Task<bool> ExistsByNameAsync(string organizationName) =>
        await db.Tenants.AnyAsync(t => t.OrganizationName.ToLower() == organizationName.ToLower());

    public void Add(Tenant tenant) => db.Tenants.Add(tenant);
}
