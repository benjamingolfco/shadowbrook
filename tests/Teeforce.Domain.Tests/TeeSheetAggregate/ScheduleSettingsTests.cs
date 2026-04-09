using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeSheetAggregate;

public class ScheduleSettingsTests
{
    [Fact]
    public void Constructor_AcceptsValidValues()
    {
        var settings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 4);

        Assert.Equal(new TimeOnly(7, 0), settings.FirstTeeTime);
        Assert.Equal(new TimeOnly(18, 0), settings.LastTeeTime);
        Assert.Equal(10, settings.IntervalMinutes);
        Assert.Equal(4, settings.DefaultCapacity);
    }

    [Fact]
    public void Constructor_ThrowsWhenFirstNotBeforeLast()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(18, 0), new TimeOnly(7, 0), 10, 4));
    }

    [Fact]
    public void Constructor_ThrowsWhenFirstEqualsLast()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(10, 0), new TimeOnly(10, 0), 10, 4));
    }

    [Fact]
    public void Constructor_ThrowsWhenIntervalNonPositive()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 0, 4));
    }

    [Fact]
    public void Constructor_ThrowsWhenDefaultCapacityNonPositive()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 0));
    }

    [Fact]
    public void Equals_TrueForSameValues()
    {
        var a = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 4);
        var b = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 4);
        Assert.Equal(a, b);
    }
}
