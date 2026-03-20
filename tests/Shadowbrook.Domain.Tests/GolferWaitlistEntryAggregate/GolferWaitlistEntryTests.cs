using NSubstitute;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Domain.Tests.GolferWaitlistEntryAggregate;

public class GolferWaitlistEntryTests
{
    // GolferWaitlistEntry is only created through WalkUpWaitlist.Join()
    // since its constructor is internal to the Domain assembly.
    private static async Task<(WalkUpWaitlist Waitlist, GolferWaitlistEntry Entry)> JoinAsync(
        Golfer? golfer = null, int groupSize = 1)
    {
        var waitlistRepo = Substitute.For<IWalkUpWaitlistRepository>();
        waitlistRepo.GetByCourseDateAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>())
            .Returns((WalkUpWaitlist?)null);

        var shortCode = Substitute.For<IShortCodeGenerator>();
        shortCode.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");

        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), shortCode, waitlistRepo);

        var entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
        entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);

        golfer ??= Golfer.Create("+15559990000", "Test", "Golfer");
        var entry = await waitlist.Join(golfer, entryRepo, groupSize);
        return (waitlist, entry);
    }

    [Fact]
    public async Task Join_SetsAllProperties()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");

        var before = DateTimeOffset.UtcNow;
        var (waitlist, entry) = await JoinAsync(golfer, groupSize: 3);
        var after = DateTimeOffset.UtcNow;

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(waitlist.Id, entry.CourseWaitlistId);
        Assert.Equal(golfer.Id, entry.GolferId);
        Assert.Equal(3, entry.GroupSize);
        Assert.True(entry.IsWalkUp);
        Assert.True(entry.IsReady);
        Assert.InRange(entry.JoinedAt, before, after);
        Assert.Null(entry.RemovedAt);
    }

    [Fact]
    public async Task Join_DefaultGroupSize_IsOne()
    {
        var (_, entry) = await JoinAsync();

        Assert.Equal(1, entry.GroupSize);
    }

    [Fact]
    public async Task Remove_SetsRemovedAt()
    {
        var (_, entry) = await JoinAsync();

        entry.Remove();

        Assert.NotNull(entry.RemovedAt);
    }

    [Fact]
    public async Task Remove_RaisesGolferRemovedFromWaitlistEvent()
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
    public async Task Remove_CalledTwice_RaisesTwoEvents()
    {
        // Documents current behavior: Remove() is not idempotent —
        // calling it twice sets RemovedAt again and raises a second event.
        var (_, entry) = await JoinAsync();

        entry.Remove();
        entry.Remove();

        Assert.Equal(2, entry.DomainEvents.Count);
        Assert.All(entry.DomainEvents, e => Assert.IsType<GolferRemovedFromWaitlist>(e));
    }
}
