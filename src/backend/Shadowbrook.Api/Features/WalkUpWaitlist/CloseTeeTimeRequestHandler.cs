using Shadowbrook.Domain.TeeTimeRequestAggregate;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public static class CloseTeeTimeRequestHandler
{
    public static async Task Handle(
        CloseTeeTimeRequest command,
        ITeeTimeRequestRepository requestRepository)
    {
        var request = await requestRepository.GetByIdAsync(command.TeeTimeRequestId);
        if (request is null)
        {
            return;
        }

        request.Close();
    }
}
