using NSubstitute;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.WaitlistOfferAggregate;

public class WaitlistOfferTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepository = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public WaitlistOfferTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");
        this.entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(new DateOnly(2026, 3, 25));
    }

    private async Task<GolferWaitlistEntry> CreateEntryAsync(int groupSize = 2)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 25),
            this.shortCodeGenerator, this.waitlistRepository, this.timeProvider);

        var golfer = Golfer.Create("+15551234567", "Test", "Golfer");
        return await waitlist.Join(golfer, this.entryRepository, this.timeProvider, "UTC", groupSize);
    }

    private TeeTimeOpening CreateOpening(Guid? courseId = null, DateOnly? date = null, TimeOnly? teeTime = null) =>
        TeeTimeOpening.Create(
            courseId ?? Guid.NewGuid(),
            date ?? new DateOnly(2026, 3, 25),
            teeTime ?? new TimeOnly(10, 0),
            slotsAvailable: 4,
            operatorOwned: false,
            this.timeProvider);

    private async Task<WaitlistOffer> CreateOfferAsync(TeeTimeOpening? opening = null)
    {
        var entry = await CreateEntryAsync();
        return entry.CreateOffer(opening ?? CreateOpening(), this.timeProvider);
    }

    [Fact]
    public async Task Create_SetsPropertiesAndGeneratesIds()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 25);
        var teeTime = new TimeOnly(10, 0);
        var entry = await CreateEntryAsync(groupSize: 2);
        var opening = CreateOpening(courseId: courseId, date: date, teeTime: teeTime);

        var offer = entry.CreateOffer(opening, this.timeProvider);

        Assert.NotEqual(Guid.Empty, offer.Id);
        Assert.NotEqual(Guid.Empty, offer.BookingId);
        Assert.NotEqual(Guid.Empty, offer.Token);
        Assert.Equal(opening.Id, offer.OpeningId);
        Assert.Equal(entry.Id, offer.GolferWaitlistEntryId);
        Assert.Equal(entry.GolferId, offer.GolferId);
        Assert.Equal(2, offer.GroupSize);
        Assert.True(offer.IsWalkUp);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Null(offer.RejectionReason);
        Assert.Equal(courseId, offer.CourseId);
        Assert.Equal(date, offer.Date);
        Assert.Equal(teeTime, offer.TeeTime);
    }

    [Fact]
    public async Task Create_RaisesWaitlistOfferCreatedEvent()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 25);
        var teeTime = new TimeOnly(10, 0);
        var entry = await CreateEntryAsync(groupSize: 2);
        var opening = CreateOpening(courseId: courseId, date: date, teeTime: teeTime);

        var offer = entry.CreateOffer(opening, this.timeProvider);

        var domainEvent = Assert.Single(offer.DomainEvents);
        var created = Assert.IsType<WaitlistOfferCreated>(domainEvent);
        Assert.Equal(offer.Id, created.WaitlistOfferId);
        Assert.Equal(offer.BookingId, created.BookingId);
        Assert.Equal(opening.Id, created.OpeningId);
        Assert.Equal(entry.Id, created.GolferWaitlistEntryId);
        Assert.Equal(entry.GolferId, created.GolferId);
        Assert.Equal(2, created.GroupSize);
        Assert.True(created.IsWalkUp);
        Assert.Equal(courseId, created.CourseId);
        Assert.Equal(date, created.Date);
        Assert.Equal(teeTime, created.TeeTime);
    }

    [Fact]
    public async Task Reject_PendingOffer_SetsRejectedWithReason()
    {
        var offer = await CreateOfferAsync();
        offer.ClearDomainEvents();

        offer.Reject("Tee time has been filled.");

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Tee time has been filled.", offer.RejectionReason);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var rejected = Assert.IsType<WaitlistOfferRejected>(domainEvent);
        Assert.Equal(offer.Id, rejected.WaitlistOfferId);
        Assert.Equal(offer.OpeningId, rejected.OpeningId);
        Assert.Equal("Tee time has been filled.", rejected.Reason);
    }

    [Fact]
    public async Task MarkNotified_SetsNotifiedAtAndRaisesEvent()
    {
        var opening = CreateOpening();
        var offer = await CreateOfferAsync(opening);
        offer.ClearDomainEvents();

        offer.MarkNotified(this.timeProvider);

        Assert.NotNull(offer.NotifiedAt);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var sent = Assert.IsType<WaitlistOfferSent>(domainEvent);
        Assert.Equal(offer.Id, sent.WaitlistOfferId);
        Assert.Equal(opening.Id, sent.OpeningId);
    }

    [Fact]
    public async Task MarkNotified_AlreadyNotified_Throws()
    {
        var offer = await CreateOfferAsync();
        offer.MarkNotified(this.timeProvider);

        Assert.Throws<OfferAlreadyNotifiedException>(() => offer.MarkNotified(this.timeProvider));
    }

    [Fact]
    public async Task Create_SetsIsStaleToFalse()
    {
        var offer = await CreateOfferAsync();

        Assert.False(offer.IsStale);
    }

    [Fact]
    public async Task MarkStale_PendingOffer_SetsIsStaleAndRaisesEvent()
    {
        var offer = await CreateOfferAsync();
        offer.ClearDomainEvents();

        offer.MarkStale();

        Assert.True(offer.IsStale);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var stale = Assert.IsType<WaitlistOfferStale>(domainEvent);
        Assert.Equal(offer.Id, stale.WaitlistOfferId);
        Assert.Equal(offer.OpeningId, stale.OpeningId);
    }

    [Fact]
    public async Task MarkStale_AlreadyStale_IsIdempotent()
    {
        var offer = await CreateOfferAsync();
        offer.MarkStale();
        offer.ClearDomainEvents();

        offer.MarkStale();

        Assert.True(offer.IsStale);
        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public async Task MarkStale_RejectedOffer_IsIdempotent()
    {
        var offer = await CreateOfferAsync();
        offer.Reject("taken");
        offer.ClearDomainEvents();

        offer.MarkStale();

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Empty(offer.DomainEvents);
    }
}
