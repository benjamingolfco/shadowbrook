using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Pricing.Handlers;
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.CoursePricingAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate;
using DomainTeeSheet = Teeforce.Domain.TeeSheetAggregate.TeeSheet;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Api.Tests.Features.Pricing.Handlers;

public class RepriceFutureSheetsHandlerTests
{
    private readonly ICoursePricingSettingsRepository pricingRepo = Substitute.For<ICoursePricingSettingsRepository>();
    private readonly ITeeSheetRepository teeSheetRepo = Substitute.For<ITeeSheetRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    public RepriceFutureSheetsHandlerTests()
    {
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 6, 1));
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_RepricesFutureSheets()
    {
        var courseId = Guid.NewGuid();
        var settings = CoursePricingSettings.Create(courseId, defaultPrice: 50m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(7, 0), new TimeOnly(12, 0), 75m);
        settings.ClearDomainEvents();

        var scheduleSettings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 10, 4);
        var sheet = DomainTeeSheet.Draft(courseId, new DateOnly(2026, 6, 1), scheduleSettings, this.timeProvider);

        this.pricingRepo.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns(settings);
        this.teeSheetRepo.GetFutureByCourseAsync(courseId, new DateOnly(2026, 6, 1), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<DomainTeeSheet> { sheet }));

        var evt = new PricingSettingsChanged { CourseId = courseId };

        await RepriceFutureSheetsHandler.Handle(
            evt, this.pricingRepo, this.teeSheetRepo, this.timeProvider, this.logger, CancellationToken.None);

        Assert.All(sheet.Intervals, i => Assert.Equal(75m, i.Price));
    }

    [Fact]
    public async Task Handle_NoPricingSettings_LogsAndReturns()
    {
        var courseId = Guid.NewGuid();
        this.pricingRepo.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns((CoursePricingSettings?)null);

        var evt = new PricingSettingsChanged { CourseId = courseId };

        await RepriceFutureSheetsHandler.Handle(
            evt, this.pricingRepo, this.teeSheetRepo, this.timeProvider, this.logger, CancellationToken.None);

        await this.teeSheetRepo.DidNotReceive().GetFutureByCourseAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoFutureSheets_CompletesWithoutError()
    {
        var courseId = Guid.NewGuid();
        var settings = CoursePricingSettings.Create(courseId, defaultPrice: 50m);
        settings.ClearDomainEvents();

        this.pricingRepo.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns(settings);
        this.teeSheetRepo.GetFutureByCourseAsync(courseId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<DomainTeeSheet>()));

        var evt = new PricingSettingsChanged { CourseId = courseId };

        await RepriceFutureSheetsHandler.Handle(
            evt, this.pricingRepo, this.teeSheetRepo, this.timeProvider, this.logger, CancellationToken.None);
    }
}
