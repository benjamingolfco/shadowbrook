using NSubstitute;
using Teeforce.Api.Infrastructure.Services;

namespace Teeforce.Api.Tests.Services;

public class DeliverEmailHandlerTests
{
    private readonly IEmailSender emailSender = Substitute.For<IEmailSender>();

    [Fact]
    public async Task Handle_SendsEmailWithSubjectAndBody()
    {
        var command = new DeliverEmail("golfer@example.com", "Booking Confirmed", "You're booked!");

        await DeliverEmailHandler.Handle(command, this.emailSender, CancellationToken.None);

        await this.emailSender.Received(1).Send("golfer@example.com", "Booking Confirmed", "You're booked!", Arg.Any<CancellationToken>());
    }
}
