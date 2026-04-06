using Microsoft.Extensions.Options;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class WaitlistOfferCreatedSendNotificationHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public WaitlistOfferCreatedSendNotificationHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private async Task<(WaitlistOffer offer, TeeTimeOpening opening)> BuildOfferAndOpeningAsync(Guid? courseId = null)
    {
        var opening = WaitlistTestHelpers.CreateOpening(
            this.timeProvider,
            courseId: courseId ?? Guid.NewGuid(),
            date: new DateOnly(2026, 8, 10),
            teeTime: new TimeOnly(8, 30));

        var offer = await WaitlistTestHelpers.CreateOfferAsync(this.timeProvider, opening);
        offer.ClearDomainEvents();

        return (offer, opening);
    }

    private static WaitlistOfferCreated BuildEvent(WaitlistOffer offer, TeeTimeOpening opening) =>
        new()
        {
            WaitlistOfferId = offer.Id,
            BookingId = Guid.CreateVersion7(),
            OpeningId = opening.Id,
            GolferWaitlistEntryId = offer.GolferWaitlistEntryId,
            GolferId = offer.GolferId,
            GroupSize = offer.GroupSize,
            IsWalkUp = offer.IsWalkUp,
            CourseId = opening.CourseId,
            Date = opening.TeeTime.Date,
            TeeTime = opening.TeeTime.Time
        };

    [Fact]
    public async Task Handle_Success_SendsNotificationWithCourseNameTeeTimeAndClaimUrl()
    {
        var (offer, opening) = await BuildOfferAndOpeningAsync();
        var course = Course.Create(opening.CourseId, "Pebble Beach", "America/Los_Angeles");
        var evt = BuildEvent(offer, opening);

        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.courseRepo.GetByIdAsync(opening.CourseId).Returns(course);

        var appSettings = Options.Create(new AppSettings { FrontendUrl = "https://test.example.com" });

        await WaitlistOfferCreatedSendNotificationHandler.Handle(
            evt, this.offerRepo, this.openingRepo, this.courseRepo, this.notificationService,
            appSettings, this.timeProvider, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            offer.GolferId,
            Arg.Is<WaitlistOfferAvailable>(n =>
                n.CourseName == "Pebble Beach" &&
                n.Time == new TimeOnly(8, 30) &&
                n.ClaimUrl.StartsWith("https://test.example.com")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FrontendUrlNotConfigured_ThrowsInvalidOperationException()
    {
        var (offer, opening) = await BuildOfferAndOpeningAsync();
        var course = Course.Create(opening.CourseId, "Pebble Beach", "America/Los_Angeles");
        var evt = BuildEvent(offer, opening);

        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.courseRepo.GetByIdAsync(opening.CourseId).Returns(course);

        var appSettings = Options.Create(new AppSettings { FrontendUrl = "" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WaitlistOfferCreatedSendNotificationHandler.Handle(
                evt, this.offerRepo, this.openingRepo, this.courseRepo, this.notificationService,
                appSettings, this.timeProvider, CancellationToken.None));

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<WaitlistOfferAvailable>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CourseNotFound_SendsNotificationWithDefaultCourseName()
    {
        var (offer, opening) = await BuildOfferAndOpeningAsync();
        var evt = BuildEvent(offer, opening);

        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.courseRepo.GetByIdAsync(opening.CourseId).Returns((Course?)null);

        var appSettings = Options.Create(new AppSettings { FrontendUrl = "https://test.example.com" });

        await WaitlistOfferCreatedSendNotificationHandler.Handle(
            evt, this.offerRepo, this.openingRepo, this.courseRepo, this.notificationService,
            appSettings, this.timeProvider, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            offer.GolferId,
            Arg.Is<WaitlistOfferAvailable>(n => n.CourseName == "Course"),
            Arg.Any<CancellationToken>());
    }
}
