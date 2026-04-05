namespace Teeforce.Domain.Common;

public class EntityNotFoundException(string entityName, Guid id)
    : DomainException($"{entityName} {id} not found.")
{
    public string EntityName { get; } = entityName;
    public Guid EntityId { get; } = id;
}
