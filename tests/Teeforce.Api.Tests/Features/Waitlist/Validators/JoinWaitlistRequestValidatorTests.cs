using Teeforce.Api.Features.Waitlist.Endpoints;

namespace Teeforce.Api.Tests.Features.Waitlist.Validators;

public class JoinWaitlistRequestValidatorTests
{
    private readonly JoinWaitlistRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", "555-123-4567", 1)).IsValid);

    [Fact]
    public void EmptyFirstName_Fails()
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "", "Smith", "555-123-4567", 1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void EmptyLastName_Fails()
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "", "555-123-4567", 1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "LastName");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("abcdefghij")]
    public void InvalidPhone_Fails(string phone)
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", phone, 1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Phone");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void PartySize_BoundaryValues_Pass(int partySize)
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", "555-123-4567", partySize));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void PartySize_OutOfRange_Fails(int partySize)
    {
        var result = this.validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", "555-123-4567", partySize));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PartySize");
    }
}
