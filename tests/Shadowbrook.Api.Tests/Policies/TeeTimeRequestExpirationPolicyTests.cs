using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class TeeTimeRequestExpirationPolicyTests
{
    [Fact]
    public void Start_TeeTimeRequestAdded_SchedulesTimeout()
    {
        var requestId = Guid.NewGuid();
        var evt = new TeeTimeRequestAdded
        {
            TeeTimeRequestId = requestId,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            GolfersNeeded = 2
        };

        var (policy, timeout) = TeeTimeRequestExpirationPolicy.Start(evt);

        Assert.Equal(requestId, policy.Id);
        Assert.Equal(requestId, timeout.TeeTimeRequestId);
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
