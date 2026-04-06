using NSubstitute;
using Teeforce.Api.Infrastructure.Services;

namespace Teeforce.Api.Tests.Services;

public class DeliverSmsHandlerTests
{
    private readonly ISmsSender smsSender = Substitute.For<ISmsSender>();

    [Fact]
    public async Task Handle_SendsSmsToPhoneNumber()
    {
        var command = new DeliverSms("+15551234567", "Your tee time is confirmed.");

        await DeliverSmsHandler.Handle(command, this.smsSender, CancellationToken.None);

        await this.smsSender.Received(1).Send("+15551234567", "Your tee time is confirmed.", Arg.Any<CancellationToken>());
    }
}
