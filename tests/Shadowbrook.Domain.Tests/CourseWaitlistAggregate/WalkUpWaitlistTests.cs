using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Domain.Tests.CourseWaitlistAggregate;

public class WalkUpWaitlistTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository repository = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public WalkUpWaitlistTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 3, 25));
    }

    [Fact]
    public async Task OpenAsync_CreatesOpenWaitlist()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository, this.timeProvider);

        Assert.NotEqual(Guid.Empty, waitlist.Id);
        Assert.Equal(courseId, waitlist.CourseId);
        Assert.Equal(date, waitlist.Date);
        Assert.Equal("1234", waitlist.ShortCode);
        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
        Assert.Contains(waitlist.DomainEvents, e => e is WalkUpWaitlistOpened opened && opened.CourseWaitlistId == waitlist.Id);
    }

    [Fact]
    public async Task OpenAsync_WhenOpenWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository, this.timeProvider);
        this.repository.GetByCourseDateAsync(courseId, date).Returns(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => WalkUpWaitlist.OpenAsync(courseId, date, this.shortCodeGenerator, this.repository, this.timeProvider));

        Assert.Equal(WaitlistStatus.Open, ex.ExistingStatus);
        Assert.Contains("already open", ex.Message);
    }

    [Fact]
    public async Task OpenAsync_WhenClosedWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository, this.timeProvider);
        existing.Close(this.timeProvider);
        this.repository.GetByCourseDateAsync(courseId, date).Returns(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => WalkUpWaitlist.OpenAsync(courseId, date, this.shortCodeGenerator, this.repository, this.timeProvider));

        Assert.Equal(WaitlistStatus.Closed, ex.ExistingStatus);
        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public async Task Close_TransitionsToClosedStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        waitlist.Close(this.timeProvider);

        Assert.Equal(WaitlistStatus.Closed, waitlist.Status);
        Assert.NotNull(waitlist.ClosedAt);
        Assert.Contains(waitlist.DomainEvents, e => e is WalkUpWaitlistClosed closed && closed.CourseWaitlistId == waitlist.Id);
    }

    [Fact]
    public async Task Close_WhenAlreadyClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close(this.timeProvider);

        Assert.Throws<WaitlistNotOpenException>(() => waitlist.Close(this.timeProvider));
    }

    [Fact]
    public async Task Reopen_WhenClosed_TransitionsToOpenStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close(this.timeProvider);

        waitlist.Reopen();

        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
        Assert.Contains(waitlist.DomainEvents, e => e is WalkUpWaitlistReopened reopened && reopened.CourseWaitlistId == waitlist.Id);
    }

    [Fact]
    public async Task Reopen_WhenAlreadyOpen_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        Assert.Throws<WaitlistNotClosedException>(() => waitlist.Reopen());
    }

    [Fact]
    public async Task Join_WhenOpen_CreatesWalkUpEntry()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");

        var entry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC", groupSize: 2);

        var walkUpEntry = Assert.IsType<WalkUpGolferWaitlistEntry>(entry);
        Assert.NotEqual(Guid.Empty, walkUpEntry.Id);
        Assert.Equal(waitlist.Id, walkUpEntry.CourseWaitlistId);
        Assert.Equal(golfer.Id, walkUpEntry.GolferId);
        Assert.Equal(2, walkUpEntry.GroupSize);
        Assert.True(walkUpEntry.IsWalkUp);
        Assert.Equal(new TimeOnly(10, 0), walkUpEntry.WindowStart);
        Assert.Equal(new TimeOnly(10, 30), walkUpEntry.WindowEnd);
    }

    [Fact]
    public async Task Join_WhenOpen_RaisesGolferJoinedWaitlist()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");

        var entry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC");

        var evt = Assert.Single(
            waitlist.DomainEvents.OfType<GolferJoinedWaitlist>(),
            e => e.CourseWaitlistId == waitlist.Id);
        Assert.Equal(entry.Id, evt.GolferWaitlistEntryId);
        Assert.Equal(golfer.Id, evt.GolferId);
    }

    [Fact]
    public async Task Join_WhenClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close(this.timeProvider);
        var golfer = Golfer.Create("+15551111111", "Joe", "Bloggs");

        await Assert.ThrowsAsync<WaitlistNotOpenException>(
            () => waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC"));
    }

    [Fact]
    public async Task Join_WhenDuplicate_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var golfer = Golfer.Create("+15552222222", "Dup", "Golfer");

        // First call succeeds; stub returns an existing entry on subsequent lookups
        var firstEntry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id)
            .Returns(firstEntry);

        await Assert.ThrowsAsync<GolferAlreadyOnWaitlistException>(
            () => waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC"));
    }

    private async Task<WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository, this.timeProvider);
    }
}
