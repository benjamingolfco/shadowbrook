using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlist;

public class WalkUpWaitlistTests
{
    private readonly StubShortCodeGenerator shortCodeGenerator = new("1234");
    private readonly StubWalkUpWaitlistRepository repository = new();

    [Fact]
    public async Task OpenAsync_CreatesOpenWaitlist()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);

        Assert.NotEqual(Guid.Empty, waitlist.Id);
        Assert.Equal(courseId, waitlist.CourseId);
        Assert.Equal(date, waitlist.Date);
        Assert.Equal("1234", waitlist.ShortCode);
        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
        Assert.Empty(waitlist.TeeTimeRequests);
    }

    [Fact]
    public async Task OpenAsync_WhenOpenWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);
        this.repository.SetExisting(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
                courseId, date, this.shortCodeGenerator, this.repository));

        Assert.Equal(WaitlistStatus.Open, ex.ExistingStatus);
        Assert.Contains("already open", ex.Message);
    }

    [Fact]
    public async Task OpenAsync_WhenClosedWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);
        existing.Close();
        this.repository.SetExisting(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
                courseId, date, this.shortCodeGenerator, this.repository));

        Assert.Equal(WaitlistStatus.Closed, ex.ExistingStatus);
        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public async Task OpenAsync_DifferentDate_Succeeds()
    {
        var courseId = Guid.NewGuid();

        var first = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);

        // Repository only returns existing for matching course+date
        // Different date = no existing = should succeed
        var second = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, new DateOnly(2026, 3, 7), this.shortCodeGenerator, this.repository);

        Assert.NotEqual(first.Id, second.Id);
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
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);
    }

    private class StubShortCodeGenerator(string code) : IShortCodeGenerator
    {
        public Task<string> GenerateAsync(DateOnly date) => Task.FromResult(code);
    }

    private class StubWalkUpWaitlistRepository : IWalkUpWaitlistRepository
    {
        private Domain.WalkUpWaitlist.WalkUpWaitlist? existing;

        public void SetExisting(Domain.WalkUpWaitlist.WalkUpWaitlist waitlist) =>
            this.existing = waitlist;

        public Task<Domain.WalkUpWaitlist.WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date)
        {
            if (this.existing is not null
                && this.existing.CourseId == courseId
                && this.existing.Date == date)
            {
                return Task.FromResult<Domain.WalkUpWaitlist.WalkUpWaitlist?>(this.existing);
            }

            return Task.FromResult<Domain.WalkUpWaitlist.WalkUpWaitlist?>(null);
        }

        public Task<Domain.WalkUpWaitlist.WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
            => Task.FromResult<Domain.WalkUpWaitlist.WalkUpWaitlist?>(null);

        public void Add(Domain.WalkUpWaitlist.WalkUpWaitlist waitlist) { }

        public Task SaveAsync() => Task.CompletedTask;
    }
}
