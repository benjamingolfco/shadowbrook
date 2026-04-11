using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Auth.Handlers;

public record CompleteIdentitySetupCommand(Guid AppUserId, string IdentityId, string FirstName, string LastName);

public static class CompleteIdentitySetupHandler
{
    public static async Task Handle(CompleteIdentitySetupCommand command, IAppUserRepository repository)
    {
        var user = await repository.GetRequiredByIdAsync(command.AppUserId);

        user.CompleteProfileSetup(command.FirstName, command.LastName);
    }
}
