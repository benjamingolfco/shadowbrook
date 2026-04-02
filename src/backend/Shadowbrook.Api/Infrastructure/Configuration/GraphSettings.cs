namespace Shadowbrook.Api.Infrastructure.Configuration;

public class GraphSettings
{
    public const string SectionName = "Graph";
    public string ClientId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }
}
