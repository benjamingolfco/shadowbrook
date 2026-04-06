namespace Teeforce.Api.Infrastructure.Services;

public record DeliverEmail(string EmailAddress, string Subject, string Body);

public static class DeliverEmailHandler
{
    public static async Task Handle(DeliverEmail command, IEmailSender emailSender, CancellationToken ct) => await emailSender.Send(command.EmailAddress, command.Subject, command.Body, ct);
}
