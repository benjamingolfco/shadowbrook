using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure.Services;

public class NoOpAppUserInvitationService(ILogger<NoOpAppUserInvitationService> logger) : IAppUserInvitationService
{
    public Task<string> SendInvitationAsync(string email, CancellationToken ct = default)
    {
        var fakeIdentityId = Guid.CreateVersion7().ToString();
        logger.LogInformation("NoOp invitation for {Email} — assigned fake IdentityId {IdentityId}. Configure Graph settings to send real invitations.", email, fakeIdentityId);
        return Task.FromResult(fakeIdentityId);
    }
}
