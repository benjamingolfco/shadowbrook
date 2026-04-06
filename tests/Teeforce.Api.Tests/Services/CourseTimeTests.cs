using Microsoft.Extensions.Time.Testing;
using Teeforce.Api.Infrastructure.Time;

namespace Teeforce.Api.Tests.Services;

public class CourseTimeTests
{
    [Fact]
    public void Today_returns_course_local_date_when_utc_is_next_day()
    {
        // 2026-03-22 01:00 UTC = 2026-03-21 20:00 Eastern (EDT, UTC-4)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 1, 0, 0, TimeSpan.Zero));

        var result = CourseTime.Today(fakeTime, TestTimeZones.NewYork);

        Assert.Equal(new DateOnly(2026, 3, 21), result);
    }

    [Fact]
    public void Today_returns_utc_date_when_course_is_utc()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 1, 0, 0, TimeSpan.Zero));

        var result = CourseTime.Today(fakeTime, TestTimeZones.Utc);

        Assert.Equal(new DateOnly(2026, 3, 22), result);
    }

    [Fact]
    public void Today_handles_dst_spring_forward()
    {
        // 2026-03-08 is spring forward in US (2:00 AM -> 3:00 AM)
        // At 2026-03-08 06:30 UTC = 2026-03-08 01:30 EST (before spring forward)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 8, 6, 30, 0, TimeSpan.Zero));

        var result = CourseTime.Today(fakeTime, TestTimeZones.NewYork);

        Assert.Equal(new DateOnly(2026, 3, 8), result);
    }

    [Fact]
    public void Today_handles_dst_fall_back()
    {
        // 2026-11-01 is fall back in US (2:00 AM -> 1:00 AM)
        // At 2026-11-01 05:30 UTC = 2026-11-01 01:30 EDT (during ambiguous hour)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));

        var result = CourseTime.Today(fakeTime, TestTimeZones.NewYork);

        Assert.Equal(new DateOnly(2026, 11, 1), result);
    }

    [Fact]
    public void Today_works_with_central_time()
    {
        // 2026-03-22 04:30 UTC = 2026-03-21 23:30 CDT (UTC-5)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 4, 30, 0, TimeSpan.Zero));

        var result = CourseTime.Today(fakeTime, TestTimeZones.Chicago);

        Assert.Equal(new DateOnly(2026, 3, 21), result);
    }

    [Fact]
    public void Today_throws_for_invalid_timezone()
    {
        var fakeTime = new FakeTimeProvider();

        Assert.Throws<TimeZoneNotFoundException>(() => CourseTime.Today(fakeTime, "Invalid/Timezone"));
    }

    [Fact]
    public void ToUtc_converts_central_time_to_utc()
    {
        // 2:30 PM Central (CDT, UTC-5) = 7:30 PM UTC
        var result = CourseTime.ToUtc(
            new DateOnly(2026, 3, 25),
            new TimeOnly(14, 30),
            TestTimeZones.Chicago);

        Assert.Equal(new DateTimeOffset(2026, 3, 25, 19, 30, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ToUtc_converts_eastern_time_to_utc()
    {
        // 7:00 PM Eastern (EDT, UTC-4) = 11:00 PM UTC
        var result = CourseTime.ToUtc(
            new DateOnly(2026, 3, 25),
            new TimeOnly(19, 0),
            TestTimeZones.NewYork);

        Assert.Equal(new DateTimeOffset(2026, 3, 25, 23, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ToUtc_handles_dst_correctly()
    {
        // 2026-03-07 is before spring forward (EST, UTC-5)
        var beforeDst = CourseTime.ToUtc(
            new DateOnly(2026, 3, 7),
            new TimeOnly(14, 0),
            TestTimeZones.NewYork);

        // 2026-03-09 is after spring forward (EDT, UTC-4)
        var afterDst = CourseTime.ToUtc(
            new DateOnly(2026, 3, 9),
            new TimeOnly(14, 0),
            TestTimeZones.NewYork);

        Assert.Equal(new DateTimeOffset(2026, 3, 7, 19, 0, 0, TimeSpan.Zero), beforeDst);
        Assert.Equal(new DateTimeOffset(2026, 3, 9, 18, 0, 0, TimeSpan.Zero), afterDst);
    }

    [Fact]
    public void Now_returns_course_local_time()
    {
        // 2026-03-22 15:30 UTC = 2026-03-22 10:30 Central (CDT, UTC-5)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 15, 30, 0, TimeSpan.Zero));

        var result = CourseTime.Now(fakeTime, TestTimeZones.Chicago);

        Assert.Equal(new TimeOnly(10, 30), result);
    }

    [Fact]
    public void Now_returns_utc_time_when_course_is_utc()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 15, 30, 45, TimeSpan.Zero));

        var result = CourseTime.Now(fakeTime, TestTimeZones.Utc);

        Assert.Equal(new TimeOnly(15, 30, 45), result);
    }

    [Fact]
    public void Now_handles_dst_spring_forward()
    {
        // 2026-03-08 06:30 UTC = 2026-03-08 01:30 EST (before spring forward at 2:00 AM)
        // After spring forward at 2:00 AM, clocks jump to 3:00 AM
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 8, 6, 30, 0, TimeSpan.Zero));

        var result = CourseTime.Now(fakeTime, TestTimeZones.NewYork);

        Assert.Equal(new TimeOnly(1, 30), result);
    }

    [Fact]
    public void Now_handles_dst_fall_back()
    {
        // 2026-11-01 06:30 UTC = 2026-11-01 01:30 EST (during ambiguous hour)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero));

        var result = CourseTime.Now(fakeTime, TestTimeZones.NewYork);

        Assert.Equal(new TimeOnly(1, 30), result);
    }

    [Fact]
    public void Now_works_with_eastern_time()
    {
        // 2026-03-22 20:15 UTC = 2026-03-22 16:15 EDT (UTC-4)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 20, 15, 0, TimeSpan.Zero));

        var result = CourseTime.Now(fakeTime, TestTimeZones.NewYork);

        Assert.Equal(new TimeOnly(16, 15), result);
    }

    [Fact]
    public void Now_throws_for_invalid_timezone()
    {
        var fakeTime = new FakeTimeProvider();

        Assert.Throws<TimeZoneNotFoundException>(() => CourseTime.Now(fakeTime, "Invalid/Timezone"));
    }
}
