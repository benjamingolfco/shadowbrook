using Microsoft.Extensions.Logging;
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.CoursePricingAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Api.Features.Pricing.Handlers;

public static class RepriceFutureSheetsHandler
{
    public static async Task Handle(
        PricingSettingsChanged evt,
        ICoursePricingSettingsRepository pricingRepository,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(evt.CourseId, ct);
        if (settings is null)
        {
            logger.LogWarning("CoursePricingSettings not found for course {CourseId}, skipping reprice", evt.CourseId);
            return;
        }

        var today = timeProvider.GetCurrentDate();
        var sheets = await teeSheetRepository.GetFutureByCourseAsync(evt.CourseId, today, ct);

        foreach (var sheet in sheets)
        {
            sheet.ApplyPricing(settings.ResolvePriceWithSource);
        }

        logger.LogInformation("Repriced {SheetCount} future tee sheets for course {CourseId}", sheets.Count, evt.CourseId);
    }
}
