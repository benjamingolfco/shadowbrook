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
    public async Task Handle_EntryNotFound_Throws()
    {
        var evt = MakeEvent();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => WaitlistOfferAcceptedSmsHandler.Handle(evt, this.entryRepo, this.golferRepo, this.sms, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GolferNotFound_Throws()
    {
        var entry = await WaitlistEntryFactory.CreateAsync();
        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => WaitlistOfferAcceptedSmsHandler.Handle(evt, this.entryRepo, this.golferRepo, this.sms, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Success_SendsSms()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = await WaitlistEntryFactory.CreateAsync(golfer);
        this.entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, this.entryRepo, this.golferRepo, this.sms, CancellationToken.None);

        await this.sms.Received(1).SendAsync(
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
