namespace Shadowbrook.Api.Infrastructure.Services;

public interface IFeatureService
{
    bool IsEnabled(string featureKey);
    Dictionary<string, bool> GetAll();
}
