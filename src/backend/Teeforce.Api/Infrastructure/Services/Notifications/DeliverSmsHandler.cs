namespace Teeforce.Api.Infrastructure.Services;

public record DeliverSms(string PhoneNumber, string Message);

public static class DeliverSmsHandler
{
    public static async Task Handle(DeliverSms command, ISmsSender smsSender, CancellationToken ct) => await smsSender.Send(command.PhoneNumber, command.Message, ct);
}
