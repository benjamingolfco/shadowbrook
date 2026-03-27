using Shadowbrook.Api.Features.FeatureFlags;

namespace Shadowbrook.Api.Infrastructure.Services;

public class FeatureService(IConfiguration configuration) : IFeatureService
{
    private readonly IConfigurationSection section = configuration.GetSection("FeatureFlags");

    public bool IsEnabled(string featureKey)
    {
        var value = this.section[featureKey];
        // Default to enabled (true) unless explicitly set to false
        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    public Dictionary<string, bool> GetAll() => FeatureKeys.All.ToDictionary(key => key, IsEnabled);
}
