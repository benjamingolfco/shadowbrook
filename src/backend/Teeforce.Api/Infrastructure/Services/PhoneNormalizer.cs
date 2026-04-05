using System.Text.RegularExpressions;

namespace Teeforce.Api.Infrastructure.Services;

public static partial class PhoneNormalizer
{
    [GeneratedRegex(@"[^\d+]")]
    private static partial Regex NonDigitOrPlusRegex();

    // Returns normalized E.164 string (+1XXXXXXXXXX) or null if invalid US number
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // Strip all non-digit characters (preserve leading + for E.164 detection)
        var stripped = NonDigitOrPlusRegex().Replace(input.Trim(), "");

        string digits;
        if (stripped.StartsWith('+'))
        {
            // Already has + prefix — strip the + and work with digits only
            digits = stripped[1..];
        }
        else
        {
            digits = stripped;
        }

        // Remove any remaining + signs (e.g., malformed "+1+5551234567")
        digits = digits.Replace("+", "");

        return digits.Length switch
        {
            10 => $"+1{digits}",
            11 when digits.StartsWith('1') => $"+{digits}",
            _ => null
        };
    }

    public static bool IsValid(string? input) => Normalize(input) is not null;
}
