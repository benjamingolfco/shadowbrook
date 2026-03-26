using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeOpeningFilledRejectOffersHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeTimeOpeningFilledRejectOffersHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_NoPendingOffers_DoesNothing()
    {
        var openingId = Guid.NewGuid();
        this.offerRepo.GetPendingByOpeningAsync(openingId).Returns(new List<WaitlistOffer>());

        var evt = new TeeTimeOpeningFilled { OpeningId = openingId };

        await TeeTimeOpeningFilledRejectOffersHandler.Handle(evt, this.offerRepo);

        await this.offerRepo.Received(1).GetPendingByOpeningAsync(openingId);
    }

    [Fact]
    public async Task Handle_PendingOffers_RejectsEach()
    {
        var openingId = Guid.NewGuid();
        var offer1 = WaitlistOffer.Create(openingId, Guid.NewGuid(), Guid.NewGuid(), 1, true, this.timeProvider);
        var offer2 = WaitlistOffer.Create(openingId, Guid.NewGuid(), Guid.NewGuid(), 2, true, this.timeProvider);
        offer1.ClearDomainEvents();
        offer2.ClearDomainEvents();

        this.offerRepo.GetPendingByOpeningAsync(openingId)
            .Returns(new List<WaitlistOffer> { offer1, offer2 });

        var evt = new TeeTimeOpeningFilled { OpeningId = openingId };

        await TeeTimeOpeningFilledRejectOffersHandler.Handle(evt, this.offerRepo);

        Assert.Equal(OfferStatus.Rejected, offer1.Status);
        Assert.Equal(OfferStatus.Rejected, offer2.Status);
        Assert.Contains(offer1.DomainEvents, e => e is WaitlistOfferRejected);
        Assert.Contains(offer2.DomainEvents, e => e is WaitlistOfferRejected);
    }
}
