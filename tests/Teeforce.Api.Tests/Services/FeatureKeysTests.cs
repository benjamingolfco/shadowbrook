using System.Reflection;
using Teeforce.Api.Features.FeatureFlags;

namespace Teeforce.Api.Tests.Services;

public class FeatureKeysTests
{
    [Fact]
    public void All_ContainsEveryPublicConstStringField()
    {
        // Get all public const string fields via reflection
        var expectedKeys = typeof(FeatureKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(k => k)
            .ToArray();

        var actualKeys = FeatureKeys.All.OrderBy(k => k).ToArray();

        Assert.Equal(expectedKeys, actualKeys);
    }
}
