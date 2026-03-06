namespace Shadowbrook.Api.Events;

public class InProcessDomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessDomainEventPublisher> _logger;

    public InProcessDomainEventPublisher(
        IServiceProvider serviceProvider,
        ILogger<InProcessDomainEventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(typeof(TEvent));
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                await ((IDomainEventHandler<TEvent>)handler!).HandleAsync(domainEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler!.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}
