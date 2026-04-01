using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Handlers;

public class TeeTimeOpeningCancelledRejectOffersHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeTimeOpeningCancelledRejectOffersHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_NoPendingOffers_DoesNothing()
    {
        var openingId = Guid.NewGuid();
        this.offerRepo.GetPendingByOpeningAsync(openingId).Returns(new List<WaitlistOffer>());

        var evt = new TeeTimeOpeningCancelled
        {
            OpeningId = openingId,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 26),
            TeeTime = new TimeOnly(10, 0),
        };

        await TeeTimeOpeningCancelledRejectOffersHandler.Handle(evt, this.offerRepo);

        await this.offerRepo.Received(1).GetPendingByOpeningAsync(openingId);
    }

    [Fact]
    public async Task Handle_PendingOffers_RejectsEachWithCancellationReason()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 26);
        var teeTime = new TimeOnly(10, 0);
        var opening = WaitlistTestHelpers.CreateOpening(this.timeProvider, courseId: courseId, date: date, teeTime: teeTime);
        var offer1 = await WaitlistTestHelpers.CreateOfferAsync(this.timeProvider, opening, groupSize: 1);
        var offer2 = await WaitlistTestHelpers.CreateOfferAsync(this.timeProvider, opening, groupSize: 2);
        offer1.ClearDomainEvents();
        offer2.ClearDomainEvents();

        this.offerRepo.GetPendingByOpeningAsync(opening.Id)
            .Returns(new List<WaitlistOffer> { offer1, offer2 });

        var evt = new TeeTimeOpeningCancelled
        {
            OpeningId = opening.Id,
            CourseId = courseId,
            Date = date,
            TeeTime = teeTime,
        };

        await TeeTimeOpeningCancelledRejectOffersHandler.Handle(evt, this.offerRepo);

        Assert.Equal(OfferStatus.Rejected, offer1.Status);
        Assert.Equal(OfferStatus.Rejected, offer2.Status);
        Assert.Equal("Tee time opening has been cancelled by the course.", offer1.RejectionReason);
        Assert.Equal("Tee time opening has been cancelled by the course.", offer2.RejectionReason);

        var rejection1 = Assert.Single(offer1.DomainEvents.OfType<WaitlistOfferRejected>());
        Assert.Equal("Tee time opening has been cancelled by the course.", rejection1.Reason);

        var rejection2 = Assert.Single(offer2.DomainEvents.OfType<WaitlistOfferRejected>());
        Assert.Equal("Tee time opening has been cancelled by the course.", rejection2.Reason);
    }
}
