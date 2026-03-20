using Xunit.Abstractions;
using Xunit.Sdk;

namespace Shadowbrook.Api.Tests;

[TraitDiscoverer("Shadowbrook.Api.Tests.IntegrationTestDiscoverer", "Shadowbrook.Api.Tests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class IntegrationTestAttribute : Attribute, ITraitAttribute;

public class IntegrationTestDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Integration");
    }
}
