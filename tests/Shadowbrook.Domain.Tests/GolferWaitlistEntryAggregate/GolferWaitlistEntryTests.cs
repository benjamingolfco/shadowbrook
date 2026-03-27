using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Exceptions;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Domain.Tests.GolferWaitlistEntryAggregate;

public class GolferWaitlistEntryTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepository = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public GolferWaitlistEntryTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero));
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 3, 25));
    }

    private async Task<(WalkUpWaitlist Waitlist, GolferWaitlistEntry Entry)> JoinAsync(
        Golfer? golfer = null, int groupSize = 1)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today),
            this.shortCodeGenerator, this.waitlistRepository, this.timeProvider);

        golfer ??= Golfer.Create("+15559990000", "Test", "Golfer");
        var entry = await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC", groupSize);
        return (waitlist, entry);
    }

    [Fact]
    public async Task Remove_SetsRemovedAt()
    {
        var (_, entry) = await JoinAsync();

        entry.Remove(this.timeProvider);

        Assert.NotNull(entry.RemovedAt);
    }

    [Fact]
    public async Task Remove_RaisesGolferRemovedFromWaitlist()
    {
        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");
        var (_, entry) = await JoinAsync(golfer);

        entry.Remove(this.timeProvider);

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

        entry.Remove(this.timeProvider);
        var firstRemovedAt = entry.RemovedAt;
        entry.Remove(this.timeProvider);

        Assert.Equal(firstRemovedAt, entry.RemovedAt);
        Assert.Single(entry.DomainEvents);
    }

    [Fact]
    public async Task ExtendWindow_WhenRemoved_Throws()
    {
        var (_, entry) = await JoinAsync();
        var walkUpEntry = Assert.IsType<WalkUpGolferWaitlistEntry>(entry);
        walkUpEntry.Remove(this.timeProvider);
        var newEnd = walkUpEntry.WindowEnd.Add(TimeSpan.FromMinutes(15));

        Assert.Throws<CannotExtendRemovedEntryException>(() => walkUpEntry.ExtendWindow(newEnd));
    }

    [Fact]
    public async Task CreateOffer_ReturnsOfferWithCorrectProperties()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var (_, entry) = await JoinAsync(golfer);
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);

        var offer = entry.CreateOffer(opening, this.timeProvider);

        Assert.Equal(opening.Id, offer.OpeningId);
        Assert.Equal(entry.Id, offer.GolferWaitlistEntryId);
        Assert.Equal(golfer.Id, offer.GolferId);
        Assert.Equal(entry.GroupSize, offer.GroupSize);
        Assert.Equal(entry.IsWalkUp, offer.IsWalkUp);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Equal(opening.CourseId, offer.CourseId);
        Assert.Equal(opening.TeeTime.Date, offer.Date);
        Assert.Equal(opening.TeeTime.Time, offer.TeeTime);
    }
}
