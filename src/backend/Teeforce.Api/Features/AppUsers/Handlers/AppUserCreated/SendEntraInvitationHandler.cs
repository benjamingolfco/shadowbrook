using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.Services;
using AppUserCreatedEvent = Teeforce.Domain.AppUserAggregate.Events.AppUserCreated;

namespace Teeforce.Api.Features.AppUsers.Handlers.AppUserCreated;

public static class SendEntraInvitationHandler
{
    public static async Task Handle(
        AppUserCreatedEvent evt,
        IAppUserRepository appUserRepository,
        IAppUserInvitationService invitationService,
        CancellationToken ct)
    {
        if (!evt.ShouldSendInvite)
        {
            return;
        }

        var appUser = await appUserRepository.GetRequiredByIdAsync(evt.AppUserId);
        await appUser.Invite(invitationService, ct);
    }
}
