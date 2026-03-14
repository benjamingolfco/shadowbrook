using Shadowbrook.Domain.WalkUpWaitlist;

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
    public void MarkFulfilled_SetsStatusToFulfilled()
    {
        var waitlistId = Guid.NewGuid();
        var teeTime = new TimeOnly(10, 0);
        var request = new TeeTimeRequest(waitlistId, teeTime, 2);

        request.MarkFulfilled();

        Assert.Equal(RequestStatus.Fulfilled, request.Status);
    }
}
