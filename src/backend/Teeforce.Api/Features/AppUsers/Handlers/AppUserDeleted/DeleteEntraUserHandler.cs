using Teeforce.Domain.Services;
using AppUserDeletedEvent = Teeforce.Domain.AppUserAggregate.Events.AppUserDeleted;

namespace Teeforce.Api.Features.AppUsers.Handlers.AppUserDeleted;

public static class DeleteEntraUserHandler
{
    public static async Task Handle(
        AppUserDeletedEvent evt,
        IAppUserDeletionService deletionService,
        ILogger logger,
        CancellationToken ct)
    {
        if (evt.IdentityId is null)
        {
            logger.LogWarning("AppUser {AppUserId} has no IdentityId, skipping Entra deletion", evt.AppUserId);
            return;
        }

        await deletionService.DeleteAsync(evt.IdentityId, ct);
    }
}
