using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Infrastructure.Events;

public class InProcessDomainEventPublisher(
    IServiceProvider serviceProvider,
    ILogger<InProcessDomainEventPublisher> logger) : IDomainEventPublisher
{
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly ILogger<InProcessDomainEventPublisher> logger = logger;

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(typeof(TEvent));
        var handlers = this.serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                await ((IDomainEventHandler<TEvent>)handler!).HandleAsync(domainEvent, ct);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler!.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}
