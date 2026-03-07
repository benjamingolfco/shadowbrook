using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlist;

public class WalkUpWaitlistTests
{
    private readonly StubShortCodeGenerator shortCodeGenerator = new("1234");

    [Fact]
    public async Task OpenAsync_CreatesOpenWaitlist()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator);

        Assert.NotEqual(Guid.Empty, waitlist.Id);
        Assert.Equal(courseId, waitlist.CourseId);
        Assert.Equal(date, waitlist.Date);
        Assert.Equal("1234", waitlist.ShortCode);
        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
        Assert.Empty(waitlist.TeeTimeRequests);
    }

    [Fact]
    public async Task Close_TransitionsToClosedStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        waitlist.Close();

        Assert.Equal(WaitlistStatus.Closed, waitlist.Status);
        Assert.NotNull(waitlist.ClosedAt);
    }

    [Fact]
    public async Task Close_WhenAlreadyClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        Assert.Throws<WaitlistNotOpenException>(() => waitlist.Close());
    }

    [Fact]
    public async Task AddTeeTimeRequest_AddsRequestAndRaisesEvent()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var teeTime = new TimeOnly(10, 0);

        var request = waitlist.AddTeeTimeRequest(teeTime, 2);

        Assert.Single(waitlist.TeeTimeRequests);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
        Assert.Equal(RequestStatus.Pending, request.Status);

        var domainEvent = Assert.Single(waitlist.DomainEvents);
        var addedEvent = Assert.IsType<TeeTimeRequestAdded>(domainEvent);
        Assert.Equal(waitlist.Id, addedEvent.WaitlistId);
        Assert.Equal(request.Id, addedEvent.TeeTimeRequestId);
        Assert.Equal(teeTime, addedEvent.TeeTime);
        Assert.Equal(2, addedEvent.GolfersNeeded);
    }

    [Fact]
    public async Task AddTeeTimeRequest_WhenClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        Assert.Throws<WaitlistNotOpenException>(
            () => waitlist.AddTeeTimeRequest(new TimeOnly(10, 0), 2));
    }

    [Fact]
    public async Task AddTeeTimeRequest_DuplicateTeeTime_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var teeTime = new TimeOnly(10, 0);
        waitlist.AddTeeTimeRequest(teeTime, 2);

        Assert.Throws<DuplicateTeeTimeRequestException>(
            () => waitlist.AddTeeTimeRequest(teeTime, 3));
    }

    [Fact]
    public async Task AddTeeTimeRequest_DifferentTeeTimes_Succeeds()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        waitlist.AddTeeTimeRequest(new TimeOnly(10, 0), 2);
        waitlist.AddTeeTimeRequest(new TimeOnly(11, 0), 3);

        Assert.Equal(2, waitlist.TeeTimeRequests.Count);
    }

    private async Task<Domain.WalkUpWaitlist.WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator);
    }

    private class StubShortCodeGenerator(string code) : IShortCodeGenerator
    {
        public Task<string> GenerateAsync(DateOnly date) => Task.FromResult(code);
    }
}
