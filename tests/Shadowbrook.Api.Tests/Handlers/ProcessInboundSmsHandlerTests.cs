using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Sms.Handlers;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Api.Tests.Handlers;

public class ProcessInboundSmsHandlerTests
{
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly ITextMessageService smsService = Substitute.For<ITextMessageService>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public ProcessInboundSmsHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_InvalidPhoneNumber_LogsWarningAndReturns()
    {
        var command = new ProcessInboundSms("invalid", "YES");

        await ProcessInboundSmsHandler.Handle(
            command,
            this.golferRepo,
            this.offerRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        await this.smsService.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownGolfer_LogsWarningAndReturns()
    {
        var command = new ProcessInboundSms("+15551234567", "YES");

        this.golferRepo.GetByPhoneAsync("+15551234567").Returns((Golfer?)null);

        await ProcessInboundSmsHandler.Handle(
            command,
            this.golferRepo,
            this.offerRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        await this.smsService.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoPendingOffer_SendsNoOffersMessage()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var command = new ProcessInboundSms("+15551234567", "YES");

        this.golferRepo.GetByPhoneAsync("+15551234567").Returns(golfer);
        this.offerRepo.GetMostRecentPendingWalkUpByGolferAsync(golfer.Id).Returns((WaitlistOffer?)null);

        await ProcessInboundSmsHandler.Handle(
            command,
            this.golferRepo,
            this.offerRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        await this.smsService.Received(1).SendAsync(
            "+15551234567",
            "You don't have any pending tee time offers right now.",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingOfferFound_AcceptsOffer()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var command = new ProcessInboundSms("+15551234567", "YES");

        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 3, 26),
            new TimeOnly(14, 0),
            slotsAvailable: 1,
            operatorOwned: true,
            this.timeProvider);

        var offer = WaitlistOffer.Create(
            opening.Id,
            Guid.NewGuid(),
            golfer.Id,
            1,
            true,
            this.timeProvider);

        this.golferRepo.GetByPhoneAsync("+15551234567").Returns(golfer);
        this.offerRepo.GetMostRecentPendingWalkUpByGolferAsync(golfer.Id).Returns(offer);

        await ProcessInboundSmsHandler.Handle(
            command,
            this.golferRepo,
            this.offerRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        // Offer should be accepted (Status changed)
        Assert.Equal(OfferStatus.Accepted, offer.Status);
        await this.smsService.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OfferAlreadyResolved_SendsNoLongerAvailableMessage()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var command = new ProcessInboundSms("+15551234567", "YES");

        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 3, 26),
            new TimeOnly(14, 0),
            slotsAvailable: 1,
            operatorOwned: true,
            this.timeProvider);

        var offer = WaitlistOffer.Create(
            opening.Id,
            Guid.NewGuid(),
            golfer.Id,
            1,
            true,
            this.timeProvider);

        // Accept the offer first to simulate race condition
        offer.Accept();
        offer.ClearDomainEvents();

        this.golferRepo.GetByPhoneAsync("+15551234567").Returns(golfer);
        this.offerRepo.GetMostRecentPendingWalkUpByGolferAsync(golfer.Id).Returns(offer);

        await ProcessInboundSmsHandler.Handle(
            command,
            this.golferRepo,
            this.offerRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        await this.smsService.Received(1).SendAsync(
            "+15551234567",
            "That tee time offer is no longer available.",
            Arg.Any<CancellationToken>());
    }
}
