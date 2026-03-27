using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class RejectStaleOfferHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public RejectStaleOfferHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private WaitlistOffer CreatePendingOffer(Guid openingId)
    {
        var offer = WaitlistOffer.Create(openingId, Guid.NewGuid(), Guid.NewGuid(), 1, true, Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(10, 0), this.timeProvider);
        offer.ClearDomainEvents();
        return offer;
    }

    [Fact]
    public async Task Handle_PendingOffer_RejectsAndReturnsStaleEvent()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new RejectStaleOffer(offer.Id, openingId);

        var result = await RejectStaleOfferHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Contains(offer.DomainEvents, e => e is WaitlistOfferRejected);
        var stale = Assert.IsType<WaitlistOfferStale>(result);
        Assert.Equal(offer.Id, stale.WaitlistOfferId);
        Assert.Equal(openingId, stale.OpeningId);
    }

    [Fact]
    public async Task Handle_AlreadyAcceptedOffer_LogsAndReturnsNull()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        offer.Accept();
        offer.ClearDomainEvents();
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new RejectStaleOffer(offer.Id, openingId);

        var result = await RejectStaleOfferHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.Null(result);
        Assert.Empty(offer.DomainEvents); // no additional events raised
    }

    [Fact]
    public async Task Handle_AlreadyRejectedOffer_LogsAndReturnsNull()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        offer.Reject("already handled");
        offer.ClearDomainEvents();
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new RejectStaleOffer(offer.Id, openingId);

        var result = await RejectStaleOfferHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.Null(result);
        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public async Task Handle_OfferNotFound_Throws()
    {
        var offerId = Guid.NewGuid();
        this.offerRepo.GetByIdAsync(offerId).Returns((WaitlistOffer?)null);

        var command = new RejectStaleOffer(offerId, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => RejectStaleOfferHandler.Handle(command, this.offerRepo, NullLogger.Instance));
    }
}
