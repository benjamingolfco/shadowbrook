using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Infrastructure.Events;

public class InProcessDomainEventPublisher(
    IServiceProvider serviceProvider,
    ILogger<InProcessDomainEventPublisher> logger) : IDomainEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(typeof(TEvent));
        var handlers = serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                await ((IDomainEventHandler<TEvent>)handler!).HandleAsync(domainEvent, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler!.GetType().Name, typeof(TEvent).Name);
            }
        }
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handlers = serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                var method = handlerType.GetMethod("HandleAsync");
                await (Task)method!.Invoke(handler, [domainEvent, ct])!;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler!.GetType().Name, eventType.Name);
            }
        }
    }
}
