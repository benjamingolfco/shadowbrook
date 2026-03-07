using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Infrastructure.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
