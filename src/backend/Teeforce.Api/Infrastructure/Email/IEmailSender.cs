namespace Teeforce.Api.Infrastructure.Email;

public interface IEmailSender
{
    Task Send(string toEmail, string subject, string body, CancellationToken ct = default);
}
