using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlistAggregate;

public class WalkUpWaitlistTests
{
    private readonly StubShortCodeGenerator shortCodeGenerator = new("1234");
    private readonly StubWalkUpWaitlistRepository repository = new();

    [Fact]
    public async Task OpenAsync_CreatesOpenWaitlist()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);

        Assert.NotEqual(Guid.Empty, waitlist.Id);
        Assert.Equal(courseId, waitlist.CourseId);
        Assert.Equal(date, waitlist.Date);
        Assert.Equal("1234", waitlist.ShortCode);
        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
    }

    [Fact]
    public async Task OpenAsync_WhenOpenWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);
        this.repository.SetExisting(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
                courseId, date, this.shortCodeGenerator, this.repository));

        Assert.Equal(WaitlistStatus.Open, ex.ExistingStatus);
        Assert.Contains("already open", ex.Message);
    }

    [Fact]
    public async Task OpenAsync_WhenClosedWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);
        existing.Close();
        this.repository.SetExisting(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
                courseId, date, this.shortCodeGenerator, this.repository));

        Assert.Equal(WaitlistStatus.Closed, ex.ExistingStatus);
        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public async Task OpenAsync_DifferentDate_Succeeds()
    {
        var courseId = Guid.NewGuid();

        var first = await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            courseId, new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);

        // Repository only returns existing for matching course+date
        // Different date = no existing = should succeed
        var second = await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
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
    public async Task AddGolfer_WhenOpen_ReturnsEntryWithCorrectProperties()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var golfer = GolferAggregate.Golfer.Create("+12125551234", "Jane", "Doe");

        var entry = waitlist.AddGolfer(golfer, groupSize: 2);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(waitlist.Id, entry.CourseWaitlistId);
        Assert.Equal(golfer.Id, entry.GolferId);
        Assert.Equal(2, entry.GroupSize);
        Assert.Null(entry.RemovedAt);
    }

    [Fact]
    public async Task AddGolfer_WhenClosed_ThrowsWaitlistNotOpenException()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();
        var golfer = GolferAggregate.Golfer.Create("+12125551234", "Jane", "Doe");

        Assert.Throws<WaitlistNotOpenException>(() => waitlist.AddGolfer(golfer));
    }

    private async Task<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);
    }

    private class StubShortCodeGenerator(string code) : IShortCodeGenerator
    {
        public Task<string> GenerateAsync(DateOnly date) => Task.FromResult(code);
    }

    private class StubWalkUpWaitlistRepository : IWalkUpWaitlistRepository
    {
        private Domain.WalkUpWaitlistAggregate.WalkUpWaitlist? existing;

        public void SetExisting(Domain.WalkUpWaitlistAggregate.WalkUpWaitlist waitlist) =>
            this.existing = waitlist;

        public Task<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date)
        {
            if (this.existing is not null
                && this.existing.CourseId == courseId
                && this.existing.Date == date)
            {
                return Task.FromResult<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?>(this.existing);
            }

            return Task.FromResult<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?>(null);
        }

        public Task<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
            => Task.FromResult<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?>(null);

        public Task<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?> GetByIdAsync(Guid id)
            => Task.FromResult<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist?>(null);

        public void Add(Domain.WalkUpWaitlistAggregate.WalkUpWaitlist waitlist) { }

        public Task SaveAsync() => Task.CompletedTask;
    }
}
