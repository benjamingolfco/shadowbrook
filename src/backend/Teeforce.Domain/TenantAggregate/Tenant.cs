using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TenantAggregate;

public class Tenant : Entity
{
    public string OrganizationName { get; private set; } = string.Empty;
    public string ContactName { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private Tenant() { } // EF

    public static Tenant Create(
        string organizationName,
        string contactName,
        string contactEmail,
        string contactPhone)
    {
        return new Tenant
        {
            Id = Guid.CreateVersion7(),
            OrganizationName = organizationName.Trim(),
            ContactName = contactName.Trim(),
            ContactEmail = contactEmail.Trim(),
            ContactPhone = contactPhone.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
