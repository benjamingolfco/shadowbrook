namespace Shadowbrook.Domain.Common;

public interface IRepository<T>
    where T : Entity
{
    Task<T?> GetByIdAsync(Guid id);
}
