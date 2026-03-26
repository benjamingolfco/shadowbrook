using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class ExpireTeeTimeOpeningHandler
{
    public static async Task Handle(ExpireTeeTimeOpening command, ITeeTimeOpeningRepository repository, ILogger logger)
    {
        var opening = await repository.GetByIdAsync(command.OpeningId);
        if (opening is null)
        {
            logger.LogWarning("TeeTimeOpening {OpeningId} not found, skipping expiration", command.OpeningId);
            return;
        }

        opening.Expire();
    }
}
