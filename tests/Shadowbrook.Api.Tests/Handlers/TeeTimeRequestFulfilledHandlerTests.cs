using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeRequestFulfilledHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();

    [Fact]
    public async Task Handle_NoPendingOffers_DoesNothing()
    {
        var requestId = Guid.NewGuid();
        offerRepo.GetPendingByRequestAsync(requestId).Returns(new List<WaitlistOffer>());

        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };
        await TeeTimeRequestFulfilledHandler.Handle(evt, offerRepo);

        offerRepo.DidNotReceive().Add(Arg.Any<WaitlistOffer>());
        offerRepo.DidNotReceive().AddRange(Arg.Any<IEnumerable<WaitlistOffer>>());
    }

    [Fact]
    public async Task Handle_PendingOffers_RejectsAll()
    {
        var requestId = Guid.NewGuid();
        var offer1 = WaitlistOffer.Create(requestId, Guid.NewGuid());
        var offer2 = WaitlistOffer.Create(requestId, Guid.NewGuid());
        offerRepo.GetPendingByRequestAsync(requestId).Returns(new List<WaitlistOffer> { offer1, offer2 });

        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };
        await TeeTimeRequestFulfilledHandler.Handle(evt, offerRepo);

        Assert.Equal(OfferStatus.Rejected, offer1.Status);
        Assert.Equal(OfferStatus.Rejected, offer2.Status);
        Assert.Equal("Tee time has been filled.", offer1.RejectionReason);
        Assert.Equal("Tee time has been filled.", offer2.RejectionReason);
    }
}
