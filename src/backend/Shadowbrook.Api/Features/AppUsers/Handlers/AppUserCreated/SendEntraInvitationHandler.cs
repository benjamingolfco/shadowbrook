using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.Services;
using AppUserCreatedEvent = Shadowbrook.Domain.AppUserAggregate.Events.AppUserCreated;

namespace Shadowbrook.Api.Features.AppUsers.Handlers.AppUserCreated;

public static class SendEntraInvitationHandler
{
    public static async Task Handle(
        AppUserCreatedEvent evt,
        IAppUserRepository appUserRepository,
        IAppUserInvitationService invitationService,
        CancellationToken ct)
    {
        var appUser = await appUserRepository.GetRequiredByIdAsync(evt.AppUserId);
        await appUser.Invite(invitationService, ct);
    }
}
