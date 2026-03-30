using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.OrganizationAggregate;

public class Organization : Entity
{
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private Organization() { } // EF

    public static Organization Create(string name)
    {
        return new Organization
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
