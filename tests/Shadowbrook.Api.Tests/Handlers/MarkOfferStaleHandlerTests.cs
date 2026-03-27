using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class MarkOfferStaleHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public MarkOfferStaleHandlerTests()
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
    public async Task Handle_PendingOffer_MarksStaleAndRaisesDomainEvent()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new MarkOfferStale(offer.Id, openingId);

        await MarkOfferStaleHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.True(offer.IsStale);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        var domainEvent = Assert.Single(offer.DomainEvents);
        var stale = Assert.IsType<WaitlistOfferStale>(domainEvent);
        Assert.Equal(offer.Id, stale.WaitlistOfferId);
        Assert.Equal(openingId, stale.OpeningId);
    }

    [Fact]
    public async Task Handle_AlreadyRejectedOffer_LogsAndSkips()
    {
        var openingId = Guid.NewGuid();
        var offer = CreatePendingOffer(openingId);
        offer.Reject("already handled");
        offer.ClearDomainEvents();
        this.offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var command = new MarkOfferStale(offer.Id, openingId);

        await MarkOfferStaleHandler.Handle(command, this.offerRepo, NullLogger.Instance);

        Assert.False(offer.IsStale);
        Assert.Empty(offer.DomainEvents);
    }

    [Fact]
    public async Task Handle_OfferNotFound_Throws()
    {
        var offerId = Guid.NewGuid();
        this.offerRepo.GetByIdAsync(offerId).Returns((WaitlistOffer?)null);

        var command = new MarkOfferStale(offerId, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => MarkOfferStaleHandler.Handle(command, this.offerRepo, NullLogger.Instance));
    }
}
