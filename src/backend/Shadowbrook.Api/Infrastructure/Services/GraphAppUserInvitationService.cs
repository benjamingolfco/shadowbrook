using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Shadowbrook.Api.Infrastructure.Configuration;
using Shadowbrook.Domain.Services;

namespace Shadowbrook.Api.Infrastructure.Services;

public class GraphAppUserInvitationService(
    GraphServiceClient graphClient,
    IOptions<AppSettings> appSettings,
    ILogger<GraphAppUserInvitationService> logger) : IAppUserInvitationService
{
    public async Task<string> SendInvitationAsync(string email, CancellationToken ct = default)
    {
        var invitation = new Invitation
        {
            InvitedUserEmailAddress = email,
            InviteRedirectUrl = appSettings.Value.FrontendUrl,
            SendInvitationMessage = true,
        };

        var result = await graphClient.Invitations.PostAsync(invitation, cancellationToken: ct);

        var identityId = result?.InvitedUser?.Id;
        if (identityId is null)
        {
            throw new InvalidOperationException($"Graph invitation for {email} returned no InvitedUser.Id");
        }

        logger.LogInformation("Sent Entra invitation to {Email}, IdentityId: {IdentityId}", email, identityId);
        return identityId;
    }
}
