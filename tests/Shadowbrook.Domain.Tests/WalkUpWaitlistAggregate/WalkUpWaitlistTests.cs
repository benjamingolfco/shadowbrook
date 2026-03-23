using NSubstitute;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Events;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlistAggregate;

public class WalkUpWaitlistTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly IWalkUpWaitlistRepository repository = Substitute.For<IWalkUpWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();

    public WalkUpWaitlistTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

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
        this.repository.GetByCourseDateAsync(courseId, date)
            .Returns(existing);

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
        this.repository.GetByCourseDateAsync(courseId, date)
            .Returns(existing);

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

        // Repository returns null for different date — NSubstitute default is null
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
    public async Task Join_WhenOpen_ReturnsEntryWithCorrectProperties()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var golfer = Golfer.Create("+12125551234", "Jane", "Doe");

        var entry = await waitlist.Join(golfer, this.entryRepository, groupSize: 2);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(waitlist.Id, entry.CourseWaitlistId);
        Assert.Equal(golfer.Id, entry.GolferId);
        Assert.Equal(2, entry.GroupSize);
        Assert.Null(entry.RemovedAt);
    }

    [Fact]
    public async Task Join_WhenClosed_ThrowsWaitlistNotOpenException()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();
        var golfer = Golfer.Create("+12125551234", "Jane", "Doe");

        await Assert.ThrowsAsync<WaitlistNotOpenException>(
            () => waitlist.Join(golfer, this.entryRepository));
    }

    [Fact]
    public async Task Join_WhenGolferAlreadyOnWaitlist_ThrowsGolferAlreadyOnWaitlistException()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var golfer = Golfer.Create("+12125551234", "Jane", "Doe");

        // First join succeeds; prime the repository to return that entry on subsequent lookups
        var firstEntry = await waitlist.Join(golfer, this.entryRepository);
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id)
            .Returns(firstEntry);

        var ex = await Assert.ThrowsAsync<GolferAlreadyOnWaitlistException>(
            () => waitlist.Join(golfer, this.entryRepository));

        Assert.Contains(golfer.Phone, ex.Message);
    }

    [Fact]
    public async Task Reopen_WhenClosed_TransitionsToOpenStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        waitlist.Reopen();

        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
    }

    [Fact]
    public async Task Reopen_WhenAlreadyOpen_ThrowsWaitlistNotClosedException()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        Assert.Throws<WaitlistNotClosedException>(() => waitlist.Reopen());
    }

    [Fact]
    public async Task Reopen_PublishesWaitlistReopenedEvent()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        waitlist.Reopen();

        Assert.Contains(waitlist.DomainEvents, e => e is WaitlistReopened reopened && reopened.CourseWaitlistId == waitlist.Id);
    }

    [Fact]
    public async Task Join_AfterReopen_Succeeds()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();
        waitlist.Reopen();

        var golfer = Golfer.Create("+12125551234", "Jane", "Doe");
        var entry = await waitlist.Join(golfer, this.entryRepository, groupSize: 1);

        Assert.NotNull(entry);
        Assert.Equal(waitlist.Id, entry.CourseWaitlistId);
        Assert.Equal(golfer.Id, entry.GolferId);
    }

    private async Task<Domain.WalkUpWaitlistAggregate.WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await Domain.WalkUpWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);
    }
}
