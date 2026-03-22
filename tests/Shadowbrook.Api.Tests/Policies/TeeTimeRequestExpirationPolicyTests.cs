using Microsoft.Extensions.Time.Testing;
using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class TeeTimeRequestExpirationPolicyTests
{
    [Fact]
    public void Start_SchedulesTimeoutWithCorrectDelay()
    {
        // 2:30 PM Chicago (CDT) = 7:30 PM UTC; current time is 5:00 PM UTC → 2.5 hour delay
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 25, 17, 0, 0, TimeSpan.Zero));
        var evt = new TeeTimeRequestAdded
        {
            TeeTimeRequestId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            GolfersNeeded = 2,
            TimeZoneId = TestTimeZones.Chicago
        };

        var (policy, timeout) = TeeTimeRequestExpirationPolicy.Start(evt, fakeTime);

        Assert.Equal(evt.TeeTimeRequestId, policy.Id);
        Assert.Equal(evt.TeeTimeRequestId, timeout.TeeTimeRequestId);
        Assert.Equal(TimeSpan.FromHours(2.5), timeout.Delay);
    }

    [Fact]
    public void Start_PastTeeTime_ClampsDelayToZero()
    {
        // Tee time is 2:30 PM Chicago (7:30 PM UTC), current time is 8:00 PM UTC → past
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 25, 20, 0, 0, TimeSpan.Zero));
        var evt = new TeeTimeRequestAdded
        {
            TeeTimeRequestId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            GolfersNeeded = 2,
            TimeZoneId = TestTimeZones.Chicago
        };

        var (_, timeout) = TeeTimeRequestExpirationPolicy.Start(evt, fakeTime);

        Assert.Equal(TimeSpan.Zero, timeout.Delay);
    }

    [Fact]
    public void Handle_ExpirationTimeout_ReturnsCloseCommandAndMarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeRequestExpirationPolicy { Id = requestId };
        var timeout = new TeeTimeRequestExpirationTimeout(requestId, TimeSpan.Zero);

        var command = policy.Handle(timeout);

        Assert.IsType<CloseTeeTimeRequest>(command);
        Assert.Equal(requestId, command.TeeTimeRequestId);
        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_TeeTimeRequestFulfilled_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeRequestExpirationPolicy { Id = requestId };
        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_TeeTimeRequestClosed_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeRequestExpirationPolicy { Id = requestId };
        var evt = new TeeTimeRequestClosed { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted());
    }
}
