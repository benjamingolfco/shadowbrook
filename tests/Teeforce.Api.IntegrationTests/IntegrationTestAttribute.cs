using Xunit.Abstractions;
using Xunit.Sdk;

namespace Teeforce.Api.IntegrationTests;

[TraitDiscoverer("Teeforce.Api.IntegrationTests.IntegrationTestDiscoverer", "Teeforce.Api.IntegrationTests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class IntegrationTestAttribute : Attribute, ITraitAttribute;

public class IntegrationTestDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Integration");
    }
}
