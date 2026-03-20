using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferRejectedSmsHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();

    [Fact]
    public async Task Handle_OfferNeverNotified_DoesNotSendSms()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var evt = MakeEvent(offerId: offer.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, offerRepo, entryRepo, golferRepo, sms, CancellationToken.None);

        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OfferNotFound_NoSms()
    {
        var evt = MakeEvent();
        await WaitlistOfferRejectedSmsHandler.Handle(evt, offerRepo, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntryNotFound_NoSms()
    {
        var offer = MakeNotifiedOffer();
        offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var evt = MakeEvent(offerId: offer.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, offerRepo, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntryAlreadyRemoved_NoSms()
    {
        var offer = MakeNotifiedOffer();
        offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entry.Remove(); // Sets RemovedAt
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(offerId: offer.Id, entryId: entry.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, offerRepo, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GolferNotFound_NoSms()
    {
        var offer = MakeNotifiedOffer();
        offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(offerId: offer.Id, entryId: entry.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, offerRepo, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsSms()
    {
        var offer = MakeNotifiedOffer();
        offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), golfer.Id);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(offerId: offer.Id, entryId: entry.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, offerRepo, entryRepo, golferRepo, sms, CancellationToken.None);

        await sms.Received(1).SendAsync(
            "+15551234567",
            Arg.Is<string>(m => m.Contains("no longer available")),
            Arg.Any<CancellationToken>());
    }

    private static WaitlistOffer MakeNotifiedOffer()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offer.MarkNotified();
        return offer;
    }

    private static WaitlistOfferRejected MakeEvent(Guid? offerId = null, Guid? entryId = null) => new()
    {
        WaitlistOfferId = offerId ?? Guid.NewGuid(),
        TeeTimeRequestId = Guid.NewGuid(),
        GolferWaitlistEntryId = entryId ?? Guid.NewGuid(),
        Reason = "Tee time has been filled."
    };
}
