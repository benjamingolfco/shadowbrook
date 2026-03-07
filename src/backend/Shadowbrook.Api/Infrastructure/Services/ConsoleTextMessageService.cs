namespace Shadowbrook.Api.Infrastructure.Services;

/// <summary>
/// Development stub that logs messages to console instead of sending real SMS.
/// Replace with a Twilio implementation when ready.
/// </summary>
public class ConsoleTextMessageService(ILogger<ConsoleTextMessageService> logger) : ITextMessageService
{
    public Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
        return Task.CompletedTask;
    }
}
