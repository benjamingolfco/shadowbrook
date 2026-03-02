namespace Shadowbrook.Api.Events;

/// <summary>
/// In-process domain event publisher. Resolves all registered handlers for the event type
/// from DI and calls them sequentially. Handler failures are caught and logged — they do not
/// fail the parent operation (per the resilience principle: core flow succeeds even if
/// downstream handlers are slow or fail).
/// </summary>
public class InProcessDomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessDomainEventPublisher> _logger;

    public InProcessDomainEventPublisher(IServiceProvider serviceProvider, ILogger<InProcessDomainEventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var handlers = _serviceProvider.GetServices<IDomainEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(domainEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Domain event handler {HandlerType} failed for event {EventType} ({EventId}). The core operation has already succeeded.",
                    handler.GetType().Name,
                    typeof(TEvent).Name,
                    domainEvent.EventId);
            }
        }
    }
}
