using Microsoft.Extensions.Logging;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Features.TeeSheet.Handlers;

public class TeeSheetUnpublishedDeleteTeeTimesHandler(
    ITeeTimeRepository teeTimeRepository,
    ILogger<TeeSheetUnpublishedDeleteTeeTimesHandler> logger)
{
    public async Task Handle(TeeSheetUnpublished evt, CancellationToken ct)
    {
        var teeTimes = await teeTimeRepository.GetByTeeSheetIdAsync(evt.TeeSheetId, ct);
        if (teeTimes.Count == 0)
        {
            logger.LogInformation("No tee times to delete for unpublished sheet {TeeSheetId}", evt.TeeSheetId);
            return;
        }

        foreach (var teeTime in teeTimes)
        {
            teeTimeRepository.Remove(teeTime);
        }

        logger.LogInformation(
            "Deleted {Count} tee time(s) for unpublished sheet {TeeSheetId}",
            teeTimes.Count,
            evt.TeeSheetId);
    }
}
