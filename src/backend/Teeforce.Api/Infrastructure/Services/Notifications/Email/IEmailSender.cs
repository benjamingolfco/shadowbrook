namespace Teeforce.Api.Infrastructure.Services;

public interface IEmailSender
{
    Task Send(string toEmail, string subject, string body, CancellationToken ct = default);
}
