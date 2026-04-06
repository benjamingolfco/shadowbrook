using Microsoft.Graph;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure;

public class GraphAppUserDeletionService(
    GraphServiceClient graphClient,
    ILogger<GraphAppUserDeletionService> logger) : IAppUserDeletionService
{
    public async Task DeleteAsync(string identityId, CancellationToken ct = default)
    {
        await graphClient.Users[identityId].DeleteAsync(cancellationToken: ct);
        logger.LogInformation("Deleted Entra user {IdentityId}", identityId);
    }
}
