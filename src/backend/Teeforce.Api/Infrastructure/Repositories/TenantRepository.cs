using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TenantAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

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
