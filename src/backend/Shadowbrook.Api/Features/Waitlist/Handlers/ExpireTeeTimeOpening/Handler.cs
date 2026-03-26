using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class ExpireTeeTimeOpeningHandler
{
    public static async Task Handle(ExpireTeeTimeOpening command, ITeeTimeOpeningRepository repository)
    {
        var opening = await repository.GetByIdAsync(command.OpeningId)
            ?? throw new InvalidOperationException($"TeeTimeOpening {command.OpeningId} not found for command {nameof(ExpireTeeTimeOpening)}.");

        opening.Expire();
    }
}
