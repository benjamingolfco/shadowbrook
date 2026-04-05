using System.Reflection;

namespace Teeforce.Api.Features.FeatureFlags;

public static class FeatureKeys
{
    public const string SmsNotifications = "sms-notifications";
    public const string DynamicPricing = "dynamic-pricing";
    public const string FullOperatorApp = "full-operator-app";

    // Reflection-based: reads all public const string fields from this class
    public static readonly string[] All = typeof(FeatureKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();
}
