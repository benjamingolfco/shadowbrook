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

    public Dictionary<string, bool> GetAllForCourse(
        Dictionary<string, bool>? orgFlags,
        Dictionary<string, bool>? courseFlags)
    {
        var result = GetAll();

        if (orgFlags is not null)
        {
            foreach (var (key, value) in orgFlags)
            {
                if (result.ContainsKey(key))
                {
                    result[key] = value;
                }
            }
        }

        if (courseFlags is not null)
        {
            foreach (var (key, value) in courseFlags)
            {
                if (result.ContainsKey(key))
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }
}
