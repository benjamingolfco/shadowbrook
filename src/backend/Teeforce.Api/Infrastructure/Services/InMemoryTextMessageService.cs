using System.Collections.Concurrent;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

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
    private readonly ConcurrentBag<SmsMessage> messages = [];

    public const string SystemPhoneNumber = "+10000000000";

    public Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var sms = new SmsMessage(SystemPhoneNumber, toPhoneNumber, message, DateTimeOffset.UtcNow, SmsDirection.Outbound);
        this.messages.Add(sms);
        logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
        return Task.CompletedTask;
    }

    public void AddInbound(string fromPhoneNumber, string message)
    {
        var sms = new SmsMessage(fromPhoneNumber, SystemPhoneNumber, message, DateTimeOffset.UtcNow, SmsDirection.Inbound);
        this.messages.Add(sms);
    }

    public IReadOnlyList<SmsMessage> GetAll() =>
        this.messages.OrderByDescending(m => m.Timestamp).ToList();

    public IReadOnlyList<SmsMessage> GetByPhone(string phoneNumber) =>
        this.messages.Where(m => m.From == phoneNumber || m.To == phoneNumber)
                .OrderBy(m => m.Timestamp)
                .ToList();

    public void Clear() => this.messages.Clear();
}
