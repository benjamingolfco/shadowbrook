using Teeforce.Domain.Common;

namespace Teeforce.Domain.TenantAggregate;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<List<Tenant>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string organizationName);
    void Add(Tenant tenant);
}
