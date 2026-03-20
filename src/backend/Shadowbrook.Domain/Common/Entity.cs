namespace Shadowbrook.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; init; }

    private readonly List<IDomainEvent> domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => this.domainEvents;
    public void ClearDomainEvents() => this.domainEvents.Clear();
    protected void AddDomainEvent(IDomainEvent domainEvent) => this.domainEvents.Add(domainEvent);
}
