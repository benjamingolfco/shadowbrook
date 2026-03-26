using Shadowbrook.Api.Features.Waitlist.Endpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class VerifyCodeRequestValidatorTests
{
    private readonly VerifyCodeRequestValidator validator = new();

    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("9999")]
    public void ValidCode_Passes(string code) =>
        Assert.True(this.validator.Validate(new VerifyCodeRequest(code)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Fails(string code) =>
        Assert.False(this.validator.Validate(new VerifyCodeRequest(code)).IsValid);

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("123")]
    [InlineData("12ab")]
    public void InvalidFormat_Fails(string code) =>
        Assert.False(this.validator.Validate(new VerifyCodeRequest(code)).IsValid);
}
