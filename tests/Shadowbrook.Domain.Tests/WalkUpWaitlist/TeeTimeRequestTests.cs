using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlist;

public class TeeTimeRequestTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var waitlistId = Guid.NewGuid();
        var teeTime = new TimeOnly(10, 0);

        var request = new TeeTimeRequest(waitlistId, teeTime, 2);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(waitlistId, request.WalkUpWaitlistId);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Fulfill_WhenPending_SetsFulfilledStatus()
    {
        var request = new TeeTimeRequest(Guid.NewGuid(), new TimeOnly(10, 0), 2);

        request.Fulfill();

        Assert.Equal(RequestStatus.Fulfilled, request.Status);
    }

    [Fact]
    public void Fulfill_WhenNotPending_ThrowsDomainException()
    {
        var request = new TeeTimeRequest(Guid.NewGuid(), new TimeOnly(10, 0), 2);
        request.Fulfill();

        Assert.Throws<TeeTimeRequestNotPendingException>(() => request.Fulfill());
    }
}
