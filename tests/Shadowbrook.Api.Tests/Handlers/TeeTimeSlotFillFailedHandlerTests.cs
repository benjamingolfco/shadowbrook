using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeSlotFillFailedHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();

    [Fact]
    public async Task Handle_OfferNotFound_DoesNothing()
    {
        var evt = new TeeTimeSlotFillFailed { TeeTimeRequestId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Reason = "test" };
        await TeeTimeSlotFillFailedHandler.Handle(evt, this.offerRepo);

        this.offerRepo.DidNotReceive().Add(Arg.Any<WaitlistOffer>());
        this.offerRepo.DidNotReceive().AddRange(Arg.Any<IEnumerable<WaitlistOffer>>());
    }

    [Fact]
    public async Task Handle_OfferFound_RejectsWithReason()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var evt = new TeeTimeSlotFillFailed { TeeTimeRequestId = Guid.NewGuid(), OfferId = offer.Id, Reason = "Group too large" };
        await TeeTimeSlotFillFailedHandler.Handle(evt, this.offerRepo);

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Group too large", offer.RejectionReason);
    }
}
