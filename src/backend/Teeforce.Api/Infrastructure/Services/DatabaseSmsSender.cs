using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Dev;

namespace Teeforce.Api.Infrastructure.Services;

public class DatabaseSmsSender(ApplicationDbContext db, ILogger<DatabaseSmsSender> logger) : ISmsSender
{
    public const string SystemPhoneNumber = "+10000000000";

    public async Task Send(string toPhoneNumber, string message, CancellationToken ct = default)
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
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
    }

    public async Task AddInbound(string fromPhoneNumber, string message, CancellationToken ct = default)
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
        await db.SaveChangesAsync(ct);
    }
}
