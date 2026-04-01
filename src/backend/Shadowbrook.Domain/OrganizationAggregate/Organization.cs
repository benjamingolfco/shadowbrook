using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.OrganizationAggregate;

public class Organization : Entity
{
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Dictionary<string, bool>? FeatureFlags { get; private set; }

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

    /// <summary>
    /// Transitional bridge: creates an Organization with an explicit ID so that it can mirror an
    /// existing Tenant row. Will be removed once the full organization/auth flow replaces legacy
    /// tenant-based course creation.
    /// </summary>
    public static Organization CreateWithId(Guid id, string name)
    {
        return new Organization
        {
            Id = id,
            Name = name.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
