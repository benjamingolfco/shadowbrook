using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

public record ExpireTeeTimeOpening(Guid OpeningId);

public static class ExpireTeeTimeOpeningHandler
{
    public static async Task Handle(ExpireTeeTimeOpening command, ITeeTimeOpeningRepository repository, ITimeProvider timeProvider)
    {
        var opening = await repository.GetRequiredByIdAsync(command.OpeningId);

        opening.Expire(timeProvider);
    }
}
