using NSubstitute;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;

namespace Shadowbrook.Domain.Tests.GolferWaitlistEntryAggregate;

public class GolferWaitlistEntryTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepository = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();

    public GolferWaitlistEntryTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
    }

    private async Task<(WalkUpWaitlist Waitlist, GolferWaitlistEntry Entry)> JoinAsync(
        Golfer? golfer = null, int groupSize = 1)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            this.shortCodeGenerator, this.waitlistRepository);

        golfer ??= Golfer.Create("+15559990000", "Test", "Golfer");
        var entry = await waitlist.Join(golfer, this.entryRepository, groupSize);
        return (waitlist, entry);
    }

    [Fact]
    public async Task Remove_SetsRemovedAt()
    {
        var (_, entry) = await JoinAsync();

        entry.Remove();

        Assert.NotNull(entry.RemovedAt);
    }

    [Fact]
    public async Task Remove_RaisesGolferRemovedFromWaitlist()
    {
        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");
        var (_, entry) = await JoinAsync(golfer);

        entry.Remove();

        var domainEvent = Assert.Single(entry.DomainEvents);
        var removed = Assert.IsType<GolferRemovedFromWaitlist>(domainEvent);
        Assert.Equal(entry.Id, removed.GolferWaitlistEntryId);
        Assert.Equal(golfer.Id, removed.GolferId);
    }

    [Fact]
    public async Task ExtendWindow_UpdatesWindowEnd()
    {
        var (_, entry) = await JoinAsync();
        var walkUpEntry = Assert.IsType<WalkUpGolferWaitlistEntry>(entry);
        var newEnd = walkUpEntry.WindowEnd.Add(TimeSpan.FromMinutes(15));

        walkUpEntry.ExtendWindow(newEnd);

        Assert.Equal(newEnd, walkUpEntry.WindowEnd);
    }

    [Fact]
    public async Task ExtendWindow_RaisesWalkUpEntryWindowExtended()
    {
        var (_, entry) = await JoinAsync();
        var walkUpEntry = Assert.IsType<WalkUpGolferWaitlistEntry>(entry);
        var newEnd = walkUpEntry.WindowEnd.Add(TimeSpan.FromMinutes(15));

        walkUpEntry.ExtendWindow(newEnd);

        var domainEvent = Assert.Single(walkUpEntry.DomainEvents);
        var evt = Assert.IsType<WalkUpEntryWindowExtended>(domainEvent);
        Assert.Equal(walkUpEntry.Id, evt.GolferWaitlistEntryId);
        Assert.Equal(newEnd, evt.NewEnd);
    }

    [Fact]
    public async Task Remove_WhenAlreadyRemoved_IsIdempotent()
    {
        var (_, entry) = await JoinAsync();

        entry.Remove();
        var firstRemovedAt = entry.RemovedAt;
        entry.Remove();

        Assert.Equal(firstRemovedAt, entry.RemovedAt);
        Assert.Single(entry.DomainEvents);
    }

    [Fact]
    public async Task ExtendWindow_WhenRemoved_Throws()
    {
        var (_, entry) = await JoinAsync();
        var walkUpEntry = Assert.IsType<WalkUpGolferWaitlistEntry>(entry);
        walkUpEntry.Remove();
        var newEnd = walkUpEntry.WindowEnd.Add(TimeSpan.FromMinutes(15));

        Assert.Throws<InvalidOperationException>(() => walkUpEntry.ExtendWindow(newEnd));
    }
}
