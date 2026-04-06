namespace Teeforce.Domain.Services;

public interface IAppUserDeletionService
{
    Task DeleteAsync(string identityId, CancellationToken ct = default);
}
