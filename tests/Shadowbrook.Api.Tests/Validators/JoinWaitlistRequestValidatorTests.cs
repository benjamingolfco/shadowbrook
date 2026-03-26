using Shadowbrook.Api.Features.Waitlist.Endpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class JoinWaitlistRequestValidatorTests
{
    private readonly JoinWaitlistRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", "555-123-4567")).IsValid);

    [Fact]
    public void EmptyFirstName_Fails()
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "", "Smith", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void EmptyLastName_Fails()
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "LastName");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("abcdefghij")]
    public void InvalidPhone_Fails(string phone)
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", phone));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Phone");
    }
}
