using Shadowbrook.Api.Infrastructure.Services;

namespace Shadowbrook.Api.Tests.Services;

public class PhoneNormalizerTests
{
    // -------------------------------------------------------------------------
    // Valid inputs -- 10-digit
    // -------------------------------------------------------------------------

    [Fact]
    public void Normalize_TenDigits_PrependsCountryCode() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("5551234567"));

    [Fact]
    public void Normalize_TenDigitsWithDashes_ReturnsE164() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("555-123-4567"));

    [Fact]
    public void Normalize_TenDigitsWithParensAndDash_ReturnsE164() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("(555) 123-4567"));

    [Fact]
    public void Normalize_TenDigitsWithDots_ReturnsE164() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("555.123.4567"));

    // -------------------------------------------------------------------------
    // Valid inputs -- 11-digit with country code
    // -------------------------------------------------------------------------

    [Fact]
    public void Normalize_ElevenDigitsStartingWithOne_ReturnsE164() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("15551234567"));

    [Fact]
    public void Normalize_AlreadyE164_ReturnsUnchanged() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("+15551234567"));

    [Fact]
    public void Normalize_E164WithSpacesAndParens_ReturnsE164() =>
        Assert.Equal("+15551234567", PhoneNormalizer.Normalize("+1 (555) 123-4567"));

    // -------------------------------------------------------------------------
    // Invalid inputs
    // -------------------------------------------------------------------------

    [Fact]
    public void Normalize_TooShort_ReturnsNull() =>
        Assert.Null(PhoneNormalizer.Normalize("123"));

    [Fact]
    public void Normalize_EmptyString_ReturnsNull() =>
        Assert.Null(PhoneNormalizer.Normalize(""));

    [Fact]
    public void Normalize_Null_ReturnsNull() =>
        Assert.Null(PhoneNormalizer.Normalize(null));

    [Fact]
    public void Normalize_TooLong_ReturnsNull() =>
        Assert.Null(PhoneNormalizer.Normalize("12345678901234"));

    [Fact]
    public void Normalize_NonNumeric_ReturnsNull() =>
        Assert.Null(PhoneNormalizer.Normalize("abcdefghij"));

    [Fact]
    public void Normalize_WhitespaceOnly_ReturnsNull() =>
        Assert.Null(PhoneNormalizer.Normalize("   "));

    // -------------------------------------------------------------------------
    // IsValid helper
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValid_ValidPhone_ReturnsTrue() =>
        Assert.True(PhoneNormalizer.IsValid("555-123-4567"));

    [Fact]
    public void IsValid_InvalidPhone_ReturnsFalse() =>
        Assert.False(PhoneNormalizer.IsValid("123"));
}
