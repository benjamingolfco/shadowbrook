namespace Teeforce.Domain.Common;

public static class RepositoryExtensions
{
    public static async Task<T> GetRequiredByIdAsync<T>(this IRepository<T> repo, Guid id)
        where T : Entity =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(typeof(T).Name, id);
}
