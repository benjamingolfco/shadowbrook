namespace Teeforce.Api.Infrastructure.Services;

public class NoOpSmsSender(ILogger<NoOpSmsSender> logger) : ISmsSender
{
    public Task Send(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        logger.LogWarning("SMS not configured. Would have sent to {PhoneNumber}: {Message}", toPhoneNumber, message);
        return Task.CompletedTask;
    }
}
