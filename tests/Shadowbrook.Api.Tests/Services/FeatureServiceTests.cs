using Microsoft.Extensions.Configuration;
using Shadowbrook.Api.Features.FeatureFlags;
using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests.Services;

public class FeatureServiceTests
{
    [Fact]
    public void IsEnabled_ReturnsTrue_WhenKeyNotConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var service = new FeatureService(config);

        var result = service.IsEnabled("sms-notifications");

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenValueIsTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:sms-notifications"] = "true"
            })
            .Build();
        var service = new FeatureService(config);

        var result = service.IsEnabled("sms-notifications");

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenValueIsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:sms-notifications"] = "false"
            })
            .Build();
        var service = new FeatureService(config);

        var result = service.IsEnabled("sms-notifications");

        Assert.False(result);
    }

    [Theory]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("false")]
    public void IsEnabled_ReturnsFalse_WhenValueIsFalse_CaseInsensitive(string value)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:sms-notifications"] = value
            })
            .Build();
        var service = new FeatureService(config);

        var result = service.IsEnabled("sms-notifications");

        Assert.False(result);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("enabled")]
    [InlineData("True")]
    public void IsEnabled_ReturnsTrue_WhenValueIsArbitraryString(string value)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:sms-notifications"] = value
            })
            .Build();
        var service = new FeatureService(config);

        var result = service.IsEnabled("sms-notifications");

        Assert.True(result);
    }

    [Fact]
    public void GetAll_ReturnsAllKeys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var service = new FeatureService(config);

        var result = service.GetAll();

        Assert.Contains("sms-notifications", result.Keys);
        Assert.Contains("dynamic-pricing", result.Keys);
        Assert.Contains("full-operator-app", result.Keys);
        Assert.Equal(FeatureKeys.All.Length, result.Count);
    }

    [Fact]
    public void GetAll_ReflectsConfiguredValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:sms-notifications"] = "false"
            })
            .Build();
        var service = new FeatureService(config);

        var result = service.GetAll();

        Assert.False(result["sms-notifications"]);
        Assert.True(result["dynamic-pricing"]);
    }

    [Fact]
    public void GetAllForCourse_CodeDefaults_WhenNoOverrides()
    {
        var service = BuildService(new());
        var result = service.GetAllForCourse(orgFlags: null, courseFlags: null);
        Assert.True(result.ContainsKey(FeatureKeys.FullOperatorApp));
    }

    [Fact]
    public void GetAllForCourse_OrgOverrides_CodeDefaults()
    {
        var service = BuildService(new());
        var orgFlags = new Dictionary<string, bool> { [FeatureKeys.FullOperatorApp] = true };
        var result = service.GetAllForCourse(orgFlags, courseFlags: null);
        Assert.True(result[FeatureKeys.FullOperatorApp]);
    }

    [Fact]
    public void GetAllForCourse_CourseOverrides_OrgFlags()
    {
        var service = BuildService(new());
        var orgFlags = new Dictionary<string, bool> { [FeatureKeys.FullOperatorApp] = false };
        var courseFlags = new Dictionary<string, bool> { [FeatureKeys.FullOperatorApp] = true };
        var result = service.GetAllForCourse(orgFlags, courseFlags);
        Assert.True(result[FeatureKeys.FullOperatorApp]);
    }

    [Fact]
    public void GetAllForCourse_OrgFlags_DoNotAffectUnrelatedKeys()
    {
        var service = BuildService(new());
        var orgFlags = new Dictionary<string, bool> { [FeatureKeys.FullOperatorApp] = true };
        var result = service.GetAllForCourse(orgFlags, courseFlags: null);
        Assert.True(result[FeatureKeys.SmsNotifications]);
    }

    private static FeatureService BuildService(Dictionary<string, string?> featureFlags)
    {
        var kvps = featureFlags.Select(kv => new KeyValuePair<string, string?>($"FeatureFlags:{kv.Key}", kv.Value));
        var config = new ConfigurationBuilder().AddInMemoryCollection(kvps).Build();
        return new FeatureService(config);
    }
}
