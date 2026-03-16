using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;

namespace Shadowbrook.Domain.Tests.GolferWaitlistEntryAggregate;

public class GolferWaitlistEntryTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var waitlistId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var before = DateTimeOffset.UtcNow;
        var entry = new GolferWaitlistEntry(waitlistId, golferId, groupSize: 3);
        var after = DateTimeOffset.UtcNow;

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(waitlistId, entry.CourseWaitlistId);
        Assert.Equal(golferId, entry.GolferId);
        Assert.Equal(3, entry.GroupSize);
        Assert.True(entry.IsWalkUp);
        Assert.True(entry.IsReady);
        Assert.InRange(entry.JoinedAt, before, after);
        Assert.Null(entry.RemovedAt);
    }

    [Fact]
    public void Constructor_DefaultGroupSize_IsOne()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(1, entry.GroupSize);
    }

    [Fact]
    public void Remove_SetsRemovedAt()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());

        entry.Remove();

        Assert.NotNull(entry.RemovedAt);
    }

    [Fact]
    public void Remove_RaisesGolferRemovedFromWaitlistEvent()
    {
        var golferId = Guid.NewGuid();
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), golferId);

        entry.Remove();

        var domainEvent = Assert.Single(entry.DomainEvents);
        var removed = Assert.IsType<GolferRemovedFromWaitlist>(domainEvent);
        Assert.Equal(entry.Id, removed.GolferWaitlistEntryId);
        Assert.Equal(golferId, removed.GolferId);
    }

    [Fact]
    public void Remove_CalledTwice_RaisesTwoEvents()
    {
        // Documents current behavior: Remove() is not idempotent —
        // calling it twice sets RemovedAt again and raises a second event.
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());

        entry.Remove();
        entry.Remove();

        Assert.Equal(2, entry.DomainEvents.Count);
        Assert.All(entry.DomainEvents, e => Assert.IsType<GolferRemovedFromWaitlist>(e));
    }
}
