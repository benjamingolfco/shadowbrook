using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Policies;

public class TeeTimeOpeningExpirationPolicyTests
{
    private readonly ICourseTimeZoneProvider courseTimeZoneProvider = Substitute.For<ICourseTimeZoneProvider>();

    public TeeTimeOpeningExpirationPolicyTests()
    {
        this.courseTimeZoneProvider.GetTimeZoneIdAsync(Arg.Any<Guid>()).Returns(TestTimeZones.Chicago);
    }

    [Fact]
    public async Task Start_SchedulesTimeoutWithCorrectDelay()
    {
        // 2:30 PM Chicago (CDT) = 7:30 PM UTC; current time is 5:00 PM UTC → 2.5 hour delay
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 25, 17, 0, 0, TimeSpan.Zero));
        var evt = new TeeTimeOpeningCreated
        {
            OpeningId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            SlotsAvailable = 3
        };

        var (policy, timeout) = await TeeTimeOpeningExpirationPolicy.Start(evt, this.courseTimeZoneProvider, fakeTime);

        Assert.Equal(evt.OpeningId, policy.Id);
        Assert.Equal(TimeSpan.FromHours(2.5), timeout.Delay);
    }

    [Fact]
    public async Task Start_PastTeeTime_ClampsDelayToZero()
    {
        // Tee time is 2:30 PM Chicago (7:30 PM UTC), current time is 8:00 PM UTC → past
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 25, 20, 0, 0, TimeSpan.Zero));
        var evt = new TeeTimeOpeningCreated
        {
            OpeningId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            SlotsAvailable = 3
        };

        var (_, timeout) = await TeeTimeOpeningExpirationPolicy.Start(evt, this.courseTimeZoneProvider, fakeTime);

        Assert.Equal(TimeSpan.Zero, timeout.Delay);
    }

    [Fact]
    public void Handle_ExpirationTimeout_ReturnsExpireCommandAndMarksCompleted()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningExpirationPolicy { Id = openingId };
        var timeout = new TeeTimeOpeningExpirationTimeout(openingId, TimeSpan.Zero);

        var command = policy.Handle(timeout);

        Assert.IsType<ExpireTeeTimeOpening>(command);
        Assert.Equal(openingId, command.OpeningId);
        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_TeeTimeOpeningFilled_MarksCompleted()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningExpirationPolicy { Id = openingId };
        var evt = new TeeTimeOpeningFilled { OpeningId = openingId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_TeeTimeOpeningExpired_MarksCompleted()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningExpirationPolicy { Id = openingId };
        var evt = new TeeTimeOpeningExpired { OpeningId = openingId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted());
    }
}
