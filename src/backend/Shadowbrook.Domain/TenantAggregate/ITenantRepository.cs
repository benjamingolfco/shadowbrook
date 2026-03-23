namespace Shadowbrook.Domain.TenantAggregate;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id);
    Task<List<Tenant>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string organizationName);
    void Add(Tenant tenant);
}
