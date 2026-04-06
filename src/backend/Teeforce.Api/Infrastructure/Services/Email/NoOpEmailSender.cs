namespace Teeforce.Api.Infrastructure.Services;

public class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task Send(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        logger.LogWarning("Email not configured. Would have sent to {Email}: {Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
