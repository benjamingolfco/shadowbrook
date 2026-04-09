using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseAggregate.Exceptions;

namespace Teeforce.Domain.Tests.CourseAggregate;

public class CourseScheduleDefaultsTests
{
    [Fact]
    public void CurrentScheduleDefaults_ReturnsSettings_WhenAllFieldsConfigured()
    {
        var course = Course.Create(Guid.NewGuid(), "Pebble", "America/Los_Angeles");
        course.UpdateTeeTimeSettings(intervalMinutes: 10, firstTeeTime: new TimeOnly(7, 0), lastTeeTime: new TimeOnly(18, 0));

        var settings = course.CurrentScheduleDefaults();

        Assert.Equal(new TimeOnly(7, 0), settings.FirstTeeTime);
        Assert.Equal(new TimeOnly(18, 0), settings.LastTeeTime);
        Assert.Equal(10, settings.IntervalMinutes);
        Assert.Equal(4, settings.DefaultCapacity); // default
    }

    [Fact]
    public void CurrentScheduleDefaults_ThrowsWhenIntervalUnset()
    {
        var course = Course.Create(Guid.NewGuid(), "Pebble", "America/Los_Angeles");

        Assert.Throws<CourseScheduleNotConfiguredException>(() => course.CurrentScheduleDefaults());
    }

    [Fact]
    public void CurrentScheduleDefaults_UsesUpdatedDefaultCapacity()
    {
        var course = Course.Create(Guid.NewGuid(), "Pebble", "America/Los_Angeles");
        course.UpdateTeeTimeSettings(10, new TimeOnly(7, 0), new TimeOnly(18, 0));
        course.UpdateDefaultCapacity(2);

        var settings = course.CurrentScheduleDefaults();

        Assert.Equal(2, settings.DefaultCapacity);
    }
}
