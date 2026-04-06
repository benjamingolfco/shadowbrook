using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class WaitlistOfferRejectedSmsHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();
    private readonly ILogger logger = Substitute.For<ILogger>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly IShortCodeGenerator shortCodeGen = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepo = Substitute.For<ICourseWaitlistRepository>();

    public WaitlistOfferRejectedSmsHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.timeProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(new DateOnly(2026, 8, 10));
        this.shortCodeGen.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC1");
    }

    private async Task<(WaitlistOffer offer, GolferWaitlistEntry entry)> BuildOfferAndEntryAsync()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 8, 10), this.shortCodeGen, this.waitlistRepo, this.timeProvider);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        var entry = await waitlist.Join(golfer, this.entryRepo, this.timeProvider, "UTC");
        entry.ClearDomainEvents();

        var opening = WaitlistTestHelpers.CreateOpening(this.timeProvider);
        var offer = entry.CreateOffer(opening, this.timeProvider);
        offer.ClearDomainEvents();

        return (offer, entry);
    }

    private static WaitlistOfferRejected BuildEvent(WaitlistOffer offer) =>
        new()
        {
            WaitlistOfferId = offer.Id,
            OpeningId = offer.OpeningId,
            GolferWaitlistEntryId = offer.GolferWaitlistEntryId,
            Reason = "Opening filled"
        };

    [Fact]
    public async Task Handle_OfferNotNotified_SkipsNotification()
    {
        var (offer, _) = await BuildOfferAndEntryAsync();
        // Offer has not been notified — NotifiedAt is null
        var evt = BuildEvent(offer);

        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        await WaitlistOfferRejectedSmsHandler.Handle(evt, this.offerRepo, this.entryRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<WaitlistOfferRejectedNotification>(), Arg.Any<CancellationToken>());
        await this.entryRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_EntryAlreadyRemoved_SkipsNotification()
    {
        var (offer, entry) = await BuildOfferAndEntryAsync();
        offer.MarkNotified(this.timeProvider);
        offer.ClearDomainEvents();

        // Remove the entry before the handler runs
        entry.Remove(this.timeProvider);
        entry.ClearDomainEvents();

        var evt = BuildEvent(offer);

        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);
        this.entryRepo.GetByIdAsync(offer.GolferWaitlistEntryId).Returns(entry);

        await WaitlistOfferRejectedSmsHandler.Handle(evt, this.offerRepo, this.entryRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<WaitlistOfferRejectedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsSorryMessageToGolfer()
    {
        var (offer, entry) = await BuildOfferAndEntryAsync();
        offer.MarkNotified(this.timeProvider);
        offer.ClearDomainEvents();

        var evt = BuildEvent(offer);
        var golferId = entry.GolferId;

        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);
        this.entryRepo.GetByIdAsync(offer.GolferWaitlistEntryId).Returns(entry);

        await WaitlistOfferRejectedSmsHandler.Handle(evt, this.offerRepo, this.entryRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Any<WaitlistOfferRejectedNotification>(),
            Arg.Any<CancellationToken>());
    }
}
