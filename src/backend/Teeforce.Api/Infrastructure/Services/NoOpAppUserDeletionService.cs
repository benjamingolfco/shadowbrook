using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure.Services;

public class NoOpAppUserDeletionService(ILogger<NoOpAppUserDeletionService> logger) : IAppUserDeletionService
{
    public Task DeleteAsync(string identityId, CancellationToken ct = default)
    {
        logger.LogInformation("NoOp deletion for IdentityId {IdentityId}. Configure Graph settings for real deletions.", identityId);
        return Task.CompletedTask;
    }
}
