using Microsoft.Extensions.Configuration;
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
        Assert.Equal(2, result.Count);
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
}
