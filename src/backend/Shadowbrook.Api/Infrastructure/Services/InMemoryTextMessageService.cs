using System.Collections.Concurrent;

namespace Shadowbrook.Api.Infrastructure.Services;

public record SmsMessage(string From, string To, string Body, DateTimeOffset Timestamp, SmsDirection Direction);

public enum SmsDirection
{
    Outbound,
    Inbound
}

/// <summary>
/// Development SMS service that captures messages in memory for inspection via /dev/sms endpoints.
/// </summary>
public class InMemoryTextMessageService(ILogger<InMemoryTextMessageService> logger) : ITextMessageService
{
    private static readonly ConcurrentBag<SmsMessage> messages = [];

    public const string SystemPhoneNumber = "+10000000000";

    public Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var sms = new SmsMessage(SystemPhoneNumber, toPhoneNumber, message, DateTimeOffset.UtcNow, SmsDirection.Outbound);
        messages.Add(sms);
        logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
        return Task.CompletedTask;
    }

    public static void AddInbound(string fromPhoneNumber, string message)
    {
        var sms = new SmsMessage(fromPhoneNumber, SystemPhoneNumber, message, DateTimeOffset.UtcNow, SmsDirection.Inbound);
        messages.Add(sms);
    }

    public static IReadOnlyList<SmsMessage> GetAll() =>
        messages.OrderByDescending(m => m.Timestamp).ToList();

    public static IReadOnlyList<SmsMessage> GetByPhone(string phoneNumber) =>
        messages.Where(m => m.From == phoneNumber || m.To == phoneNumber)
                .OrderBy(m => m.Timestamp)
                .ToList();

    public static void Clear() => messages.Clear();
}
