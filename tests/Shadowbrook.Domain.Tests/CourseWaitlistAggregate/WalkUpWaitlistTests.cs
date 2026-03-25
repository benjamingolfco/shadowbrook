using NSubstitute;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.CourseWaitlistAggregate;

public class WalkUpWaitlistTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository repository = Substitute.For<ICourseWaitlistRepository>();

    public WalkUpWaitlistTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

    [Fact]
    public async Task OpenAsync_CreatesOpenWaitlist()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);

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
            courseId, date, this.shortCodeGenerator, this.repository);
        this.repository.GetByCourseDateAsync(courseId, date).Returns(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => WalkUpWaitlist.OpenAsync(courseId, date, this.shortCodeGenerator, this.repository));

        Assert.Equal(WaitlistStatus.Open, ex.ExistingStatus);
        Assert.Contains("already open", ex.Message);
    }

    [Fact]
    public async Task OpenAsync_WhenClosedWaitlistExists_Throws()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var existing = await WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator, this.repository);
        existing.Close();
        this.repository.GetByCourseDateAsync(courseId, date).Returns(existing);

        var ex = await Assert.ThrowsAsync<WaitlistAlreadyExistsException>(
            () => WalkUpWaitlist.OpenAsync(courseId, date, this.shortCodeGenerator, this.repository));

        Assert.Equal(WaitlistStatus.Closed, ex.ExistingStatus);
        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public async Task Close_TransitionsToClosedStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        waitlist.Close();

        Assert.Equal(WaitlistStatus.Closed, waitlist.Status);
        Assert.NotNull(waitlist.ClosedAt);
        Assert.Contains(waitlist.DomainEvents, e => e is WalkUpWaitlistClosed closed && closed.CourseWaitlistId == waitlist.Id);
    }

    [Fact]
    public async Task Close_WhenAlreadyClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        Assert.Throws<WaitlistNotOpenException>(() => waitlist.Close());
    }

    [Fact]
    public async Task Reopen_WhenClosed_TransitionsToOpenStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

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

    private async Task<WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);
    }
}
