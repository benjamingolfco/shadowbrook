using Teeforce.Api.Infrastructure.Services;

namespace Teeforce.Api.Infrastructure.Dev;

public class DevSmsMessage
{
    public Guid Id { get; set; }
    public required string From { get; set; }
    public required string To { get; set; }
    public required string Body { get; set; }
    public SmsDirection Direction { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
