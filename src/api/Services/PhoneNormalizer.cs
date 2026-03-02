using System.Text.RegularExpressions;

namespace Shadowbrook.Api.Services;

/// <summary>
/// Normalizes US phone numbers to E.164 format (+16125551234).
/// Handles common formatting variants: dashes, parentheses, spaces, dots.
/// US-only for v1 — all initial courses are US-based.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Normalizes a phone number string to E.164 format.
    /// Returns null if the input cannot be normalized to a valid US number.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Strip all non-digit characters
        var digits = Regex.Replace(input, @"\D", "");

        return digits.Length switch
        {
            10 => $"+1{digits}",                                       // 6125551234  -> +16125551234
            11 when digits.StartsWith('1') => $"+{digits}",           // 16125551234 -> +16125551234
            _ => null                                                   // any other length is invalid
        };
    }

    /// <summary>
    /// Returns true if the input can be normalized to a valid US E.164 phone number.
    /// </summary>
    public static bool IsValid(string? input) => Normalize(input) is not null;
}
