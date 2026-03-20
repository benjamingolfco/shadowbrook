using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedSmsHandlerTests
{
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();

    [Fact]
    public async Task Handle_EntryNotFound_NoSms()
    {
        var evt = MakeEvent();
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GolferNotFound_NoSms()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsSms()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), golfer.Id);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);

        await sms.Received(1).SendAsync(
            "+15551234567",
            Arg.Is<string>(m => m.Contains("processing")),
            Arg.Any<CancellationToken>());
    }

    private static WaitlistOfferAccepted MakeEvent(Guid? entryId = null) => new()
    {
        WaitlistOfferId = Guid.NewGuid(),
        BookingId = Guid.CreateVersion7(),
        TeeTimeRequestId = Guid.NewGuid(),
        GolferWaitlistEntryId = entryId ?? Guid.NewGuid(),
        GolferId = Guid.NewGuid()
    };
}
