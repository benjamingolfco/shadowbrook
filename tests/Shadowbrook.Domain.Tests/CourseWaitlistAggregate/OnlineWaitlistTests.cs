using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Domain.Tests.CourseWaitlistAggregate;

public class OnlineWaitlistTests
{
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public OnlineWaitlistTests()
    {
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 3, 25));
    }

    [Fact]
    public async Task Join_CreatesOnlineEntry()
    {
        var waitlist = OnlineWaitlist.Create(Guid.NewGuid(), new DateOnly(2026, 4, 1), this.timeProvider);
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var windowStart = new DateTime(2026, 4, 1, 8, 0, 0);
        var windowEnd = new DateTime(2026, 4, 1, 10, 0, 0);

        var entry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, 2, windowStart, windowEnd);

        var onlineEntry = Assert.IsType<OnlineGolferWaitlistEntry>(entry);
        Assert.NotEqual(Guid.Empty, onlineEntry.Id);
        Assert.Equal(waitlist.Id, onlineEntry.CourseWaitlistId);
        Assert.Equal(golfer.Id, onlineEntry.GolferId);
        Assert.Equal(2, onlineEntry.GroupSize);
        Assert.False(onlineEntry.IsWalkUp);
        Assert.Equal(windowStart, onlineEntry.WindowStart);
        Assert.Equal(windowEnd, onlineEntry.WindowEnd);
    }

    [Fact]
    public async Task Join_RaisesGolferJoinedWaitlist()
    {
        var waitlist = OnlineWaitlist.Create(Guid.NewGuid(), new DateOnly(2026, 4, 1), this.timeProvider);
        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");
        var windowStart = new DateTime(2026, 4, 1, 9, 0, 0);
        var windowEnd = new DateTime(2026, 4, 1, 11, 0, 0);

        var entry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, 1, windowStart, windowEnd);

        var evt = Assert.Single(waitlist.DomainEvents.OfType<GolferJoinedWaitlist>());
        Assert.Equal(entry.Id, evt.GolferWaitlistEntryId);
        Assert.Equal(waitlist.Id, evt.CourseWaitlistId);
        Assert.Equal(golfer.Id, evt.GolferId);
    }

    [Fact]
    public async Task Join_WhenDuplicate_Throws()
    {
        var waitlist = OnlineWaitlist.Create(Guid.NewGuid(), new DateOnly(2026, 4, 1), this.timeProvider);
        var golfer = Golfer.Create("+15552222222", "Dup", "Golfer");
        var windowStart = new DateTime(2026, 4, 1, 8, 0, 0);
        var windowEnd = new DateTime(2026, 4, 1, 10, 0, 0);

        var firstEntry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, 1, windowStart, windowEnd);
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id)
            .Returns(firstEntry);

        await Assert.ThrowsAsync<GolferAlreadyOnWaitlistException>(
            () => waitlist.Join(golfer, this.entryRepository, this.timeProvider, 1, windowStart, windowEnd));
    }
}
