using NSubstitute;
using Teeforce.Domain.TeeSheetAggregate;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Domain.Tests.TeeSheetAggregate;

public class TeeSheetPricingTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeSheetPricingTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private TeeSheet CreateSheet(DateOnly? date = null)
    {
        var settings = new ScheduleSettings(
            firstTeeTime: new TimeOnly(7, 0),
            lastTeeTime: new TimeOnly(8, 0),
            intervalMinutes: 10,
            defaultCapacity: 4);

        return TeeSheet.Draft(
            Guid.NewGuid(),
            date ?? new DateOnly(2026, 6, 1),
            settings,
            this.timeProvider);
    }

    [Fact]
    public void ApplyPricing_StampsPriceAndScheduleIdOnEachInterval()
    {
        var sheet = CreateSheet();
        var scheduleId = Guid.NewGuid();

        sheet.ApplyPricing((day, time) => (50m, scheduleId));

        Assert.All(sheet.Intervals, interval =>
        {
            Assert.Equal(50m, interval.Price);
            Assert.Equal(scheduleId, interval.RateScheduleId);
        });
    }

    [Fact]
    public void ApplyPricing_NullPrice_SetsNullOnIntervals()
    {
        var sheet = CreateSheet();

        sheet.ApplyPricing((day, time) => (null, null));

        Assert.All(sheet.Intervals, interval =>
        {
            Assert.Null(interval.Price);
            Assert.Null(interval.RateScheduleId);
        });
    }

    [Fact]
    public void ApplyPricing_DifferentPricesPerTime_StampsCorrectly()
    {
        var sheet = CreateSheet();
        var earlyId = Guid.NewGuid();

        sheet.ApplyPricing((day, time) =>
            time < new TimeOnly(7, 30)
                ? (60m, earlyId)
                : (40m, null));

        var earlyIntervals = sheet.Intervals.Where(i => i.Time < new TimeOnly(7, 30)).ToList();
        var lateIntervals = sheet.Intervals.Where(i => i.Time >= new TimeOnly(7, 30)).ToList();

        Assert.All(earlyIntervals, i =>
        {
            Assert.Equal(60m, i.Price);
            Assert.Equal(earlyId, i.RateScheduleId);
        });
        Assert.All(lateIntervals, i =>
        {
            Assert.Equal(40m, i.Price);
            Assert.Null(i.RateScheduleId);
        });
    }

    [Fact]
    public void ApplyPricing_PassesCorrectDayOfWeek()
    {
        var sheet = CreateSheet(date: new DateOnly(2026, 6, 1));
        DayOfWeek? capturedDay = null;

        sheet.ApplyPricing((day, time) =>
        {
            capturedDay = day;
            return (50m, null);
        });

        Assert.Equal(DayOfWeek.Monday, capturedDay);
    }

    [Fact]
    public void Intervals_InitiallyHaveNullPricing()
    {
        var sheet = CreateSheet();

        Assert.All(sheet.Intervals, interval =>
        {
            Assert.Null(interval.Price);
            Assert.Null(interval.RateScheduleId);
        });
    }
}
