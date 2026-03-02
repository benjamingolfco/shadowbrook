using Shadowbrook.Api.Services;

namespace Shadowbrook.Api.Events.Handlers;

public class SendWaitlistConfirmationSmsHandler : IDomainEventHandler<GolferJoinedWaitlist>
{
    private readonly ITextMessageService _sms;
    private readonly ILogger<SendWaitlistConfirmationSmsHandler> _logger;

    public SendWaitlistConfirmationSmsHandler(ITextMessageService sms, ILogger<SendWaitlistConfirmationSmsHandler> logger)
    {
        _sms = sms;
        _logger = logger;
    }

    public async Task HandleAsync(GolferJoinedWaitlist domainEvent, CancellationToken ct = default)
    {
        var message = $"You're on the waitlist at {domainEvent.CourseName}! " +
                      $"Your position: #{domainEvent.Position}. " +
                      $"Keep your phone handy -- we'll text you when a tee time opens up.";

        _logger.LogInformation(
            "Sending waitlist confirmation SMS to {Phone} for entry {EntryId}",
            domainEvent.GolferPhone,
            domainEvent.GolferWaitlistEntryId);

        await _sms.SendAsync(domainEvent.GolferPhone, message, ct);
    }
}
