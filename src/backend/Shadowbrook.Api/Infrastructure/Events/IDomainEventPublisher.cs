using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Infrastructure.Events;

public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
