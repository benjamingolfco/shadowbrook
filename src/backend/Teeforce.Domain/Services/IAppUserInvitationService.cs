namespace Teeforce.Domain.Services;

public interface IAppUserInvitationService
{
    Task<string> SendInvitationAsync(string email, CancellationToken ct = default);
}
