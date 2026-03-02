using Shadowbrook.Api.Services;

namespace Shadowbrook.Api.Tests;

public class PhoneNormalizerTests
{
    [Fact]
    public void Normalize_TenDigits_PrependsPlusOne()
    {
        var result = PhoneNormalizer.Normalize("6125551234");
        Assert.Equal("+16125551234", result);
    }

    [Fact]
    public void Normalize_ElevenDigitsWithOne_PrependsPlus()
    {
        var result = PhoneNormalizer.Normalize("16125551234");
        Assert.Equal("+16125551234", result);
    }

    [Fact]
    public void Normalize_AlreadyE164_ReturnsSame()
    {
        // Strip the + to get digits, then re-normalize
        var result = PhoneNormalizer.Normalize("+16125551234");
        Assert.Equal("+16125551234", result);
    }

    [Fact]
    public void Normalize_WithDashes_StripsFormatting()
    {
        var result = PhoneNormalizer.Normalize("612-555-1234");
        Assert.Equal("+16125551234", result);
    }

    [Fact]
    public void Normalize_WithParens_StripsFormatting()
    {
        var result = PhoneNormalizer.Normalize("(612) 555-1234");
        Assert.Equal("+16125551234", result);
    }

    [Fact]
    public void Normalize_TooFewDigits_ReturnsNull()
    {
        var result = PhoneNormalizer.Normalize("12345");
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_TooManyDigits_ReturnsNull()
    {
        var result = PhoneNormalizer.Normalize("12345678901234");
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_Null_ReturnsNull()
    {
        var result = PhoneNormalizer.Normalize(null);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_Empty_ReturnsNull()
    {
        var result = PhoneNormalizer.Normalize("");
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_NonDigits_ReturnsNull()
    {
        var result = PhoneNormalizer.Normalize("abcdefghij");
        Assert.Null(result);
    }
}
