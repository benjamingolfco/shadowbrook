using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public static class ExpireTeeTimeOpeningHandler
{
    public static async Task Handle(ExpireTeeTimeOpening command, ITeeTimeOpeningRepository repository)
    {
        var opening = await repository.GetByIdAsync(command.OpeningId);
        if (opening is null)
        {
            return;
        }

        opening.Expire();
    }
}
