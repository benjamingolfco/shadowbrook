using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class BookingRejectedSmsHandlerTests
{
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ITextMessageService smsService = Substitute.For<ITextMessageService>();

    [Fact]
    public async Task Handle_GolferNotFound_LogsWarningAndReturns()
    {
        var golferId = Guid.NewGuid();
        var evt = new BookingRejected { BookingId = Guid.NewGuid(), GolferId = golferId };

        this.golferRepo.GetByIdAsync(golferId).Returns((Golfer?)null);

        await BookingRejectedSmsHandler.Handle(
            evt,
            this.golferRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        await this.smsService.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GolferFound_SendsSlotTakenMessage()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var evt = new BookingRejected { BookingId = Guid.NewGuid(), GolferId = golfer.Id };

        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        await BookingRejectedSmsHandler.Handle(
            evt,
            this.golferRepo,
            this.smsService,
            NullLogger.Instance,
            CancellationToken.None);

        await this.smsService.Received(1).SendAsync(
            "+15551234567",
            "Sorry, that tee time was claimed by another golfer. You're still on the waitlist for future openings!",
            Arg.Any<CancellationToken>());
    }
}
