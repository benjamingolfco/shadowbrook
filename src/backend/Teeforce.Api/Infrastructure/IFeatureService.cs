namespace Teeforce.Api.Infrastructure;

public interface IFeatureService
{
    bool IsEnabled(string featureKey);
    Dictionary<string, bool> GetAll();
    Dictionary<string, bool> GetAllForCourse(
        Dictionary<string, bool>? orgFlags,
        Dictionary<string, bool>? courseFlags);
}
