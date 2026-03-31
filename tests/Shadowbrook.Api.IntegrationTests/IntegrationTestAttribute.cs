using Xunit.Abstractions;
using Xunit.Sdk;

namespace Shadowbrook.Api.IntegrationTests;

[TraitDiscoverer("Shadowbrook.Api.IntegrationTests.IntegrationTestDiscoverer", "Shadowbrook.Api.IntegrationTests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class IntegrationTestAttribute : Attribute, ITraitAttribute;

public class IntegrationTestDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Integration");
    }
}
