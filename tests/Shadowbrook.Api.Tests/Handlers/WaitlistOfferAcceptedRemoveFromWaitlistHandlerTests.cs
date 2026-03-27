using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedRemoveFromWaitlistHandlerTests
{
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly IShortCodeGenerator shortCodeGen = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepo = Substitute.For<ICourseWaitlistRepository>();

    public WaitlistOfferAcceptedRemoveFromWaitlistHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.shortCodeGen.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

    private async Task<WalkUpGolferWaitlistEntry> CreateEntryAsync(Golfer golfer, int groupSize = 1)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), this.shortCodeGen, this.waitlistRepo, this.timeProvider);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        return await waitlist.Join(golfer, this.entryRepo, this.timeProvider, "UTC", groupSize);
    }

    [Fact]
    public async Task Handle_EntryExists_RemovesEntry()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var entry = await CreateEntryAsync(golfer);
        entry.ClearDomainEvents();

        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = entry.Id,
            GolferId = golfer.Id,
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0)
        };

        await WaitlistOfferAcceptedRemoveFromWaitlistHandler.Handle(evt, this.entryRepo, this.timeProvider);

        Assert.Contains(entry.DomainEvents, e => e is GolferRemovedFromWaitlist);
    }

    [Fact]
    public async Task Handle_EntryNotFound_Throws()
    {
        var entryId = Guid.NewGuid();
        this.entryRepo.GetByIdAsync(entryId).Returns((GolferWaitlistEntry?)null);

        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = entryId,
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(10, 0)
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => WaitlistOfferAcceptedRemoveFromWaitlistHandler.Handle(evt, this.entryRepo, this.timeProvider));
    }
}
