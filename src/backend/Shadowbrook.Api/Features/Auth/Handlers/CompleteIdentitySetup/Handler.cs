using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Features.Auth.Handlers;

public record CompleteIdentitySetupCommand(Guid AppUserId, string IdentityId, string FirstName, string LastName);

public static class CompleteIdentitySetupHandler
{
    public static async Task Handle(CompleteIdentitySetupCommand command, IRepository<AppUser> repository)
    {
        var user = await repository.GetRequiredByIdAsync(command.AppUserId);

        user.CompleteIdentitySetup(command.IdentityId, command.FirstName, command.LastName);
    }
}
