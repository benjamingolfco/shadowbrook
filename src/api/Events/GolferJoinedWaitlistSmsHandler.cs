using Shadowbrook.Api.Services;

namespace Shadowbrook.Api.Events;

public class GolferJoinedWaitlistSmsHandler(ITextMessageService textMessageService)
    : IDomainEventHandler<GolferJoinedWaitlist>
{
    public async Task HandleAsync(GolferJoinedWaitlist domainEvent, CancellationToken ct = default)
    {
        var message = $"You're #{domainEvent.Position} on the waitlist at {domainEvent.CourseName}. " +
                      "Keep your phone handy - we'll text you when a spot opens up!";
        await textMessageService.SendAsync(domainEvent.GolferPhone, message, ct);
    }
}
