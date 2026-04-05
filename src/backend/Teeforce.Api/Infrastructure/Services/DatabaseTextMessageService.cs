using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Dev;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

/// <summary>
/// Development SMS service that persists messages to the database for inspection via /dev/sms endpoints.
/// Survives container restarts, unlike InMemoryTextMessageService.
/// </summary>
public class DatabaseTextMessageService(ApplicationDbContext db, ILogger<DatabaseTextMessageService> logger) : ITextMessageService
{
    public const string SystemPhoneNumber = "+10000000000";

    public async Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var sms = new DevSmsMessage
        {
            Id = Guid.NewGuid(),
            From = SystemPhoneNumber,
            To = toPhoneNumber,
            Body = message,
            Direction = SmsDirection.Outbound,
            Timestamp = DateTimeOffset.UtcNow
        };

        db.DevSmsMessages.Add(sms);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
    }

    public async Task AddInboundAsync(string fromPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var sms = new DevSmsMessage
        {
            Id = Guid.NewGuid(),
            From = fromPhoneNumber,
            To = SystemPhoneNumber,
            Body = message,
            Direction = SmsDirection.Inbound,
            Timestamp = DateTimeOffset.UtcNow
        };

        db.DevSmsMessages.Add(sms);
        await db.SaveChangesAsync(cancellationToken);
    }
}
