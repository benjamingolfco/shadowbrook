using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TenantAggregate;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<List<Tenant>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string organizationName);
    void Add(Tenant tenant);
}
