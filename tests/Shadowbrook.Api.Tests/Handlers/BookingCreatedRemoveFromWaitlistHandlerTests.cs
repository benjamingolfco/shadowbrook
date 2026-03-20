using NSubstitute;
using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class BookingCreatedRemoveFromWaitlistHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();

    [Fact]
    public async Task Handle_OfferNotFound_DoesNothing()
    {
        var evt = new BookingCreated { BookingId = Guid.CreateVersion7(), GolferId = Guid.NewGuid(), CourseId = Guid.NewGuid() };
        await BookingCreatedRemoveFromWaitlistHandler.Handle(evt, this.offerRepo, this.entryRepo);

        await this.entryRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_EntryNotFound_DoesNothing()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        this.offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);

        var evt = new BookingCreated { BookingId = offer.BookingId, GolferId = Guid.NewGuid(), CourseId = Guid.NewGuid() };
        await BookingCreatedRemoveFromWaitlistHandler.Handle(evt, this.offerRepo, this.entryRepo);

        this.entryRepo.DidNotReceive().Add(Arg.Any<GolferWaitlistEntry>());
    }

    [Fact]
    public async Task Handle_Success_RemovesEntry()
    {
        var entry = await WaitlistEntryFactory.CreateAsync();
        Assert.Null(entry.RemovedAt);

        var offer = WaitlistOffer.Create(Guid.NewGuid(), entry.Id);
        this.offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);
        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = new BookingCreated { BookingId = offer.BookingId, GolferId = Guid.NewGuid(), CourseId = Guid.NewGuid() };
        await BookingCreatedRemoveFromWaitlistHandler.Handle(evt, this.offerRepo, this.entryRepo);

        Assert.NotNull(entry.RemovedAt);
    }
}
