namespace Shadowbrook.Api.Services;

/// <summary>
/// Development stub that logs messages to console instead of sending real SMS.
/// Replace with a Twilio implementation when ready.
/// </summary>
public class ConsoleTextMessageService : ITextMessageService
{
    private readonly ILogger<ConsoleTextMessageService> _logger;

    public ConsoleTextMessageService(ILogger<ConsoleTextMessageService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
        return Task.CompletedTask;
    }
}
