using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

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

        var evt = new TeeTimeOpeningCancelled { OpeningId = openingId };

        await TeeTimeOpeningCancelledRejectOffersHandler.Handle(evt, this.offerRepo);

        await this.offerRepo.Received(1).GetPendingByOpeningAsync(openingId);
    }

    [Fact]
    public async Task Handle_PendingOffers_RejectsEachWithCancellationReason()
    {
        var openingId = Guid.NewGuid();
        var offer1 = WaitlistOffer.Create(openingId, Guid.NewGuid(), Guid.NewGuid(), 1, true, this.timeProvider);
        var offer2 = WaitlistOffer.Create(openingId, Guid.NewGuid(), Guid.NewGuid(), 2, true, this.timeProvider);
        offer1.ClearDomainEvents();
        offer2.ClearDomainEvents();

        this.offerRepo.GetPendingByOpeningAsync(openingId)
            .Returns(new List<WaitlistOffer> { offer1, offer2 });

        var evt = new TeeTimeOpeningCancelled { OpeningId = openingId };

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
