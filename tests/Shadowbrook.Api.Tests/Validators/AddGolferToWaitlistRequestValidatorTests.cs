using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class AddGolferToWaitlistRequestValidatorTests
{
    private readonly AddGolferToWaitlistRequestValidator validator = new();

    [Fact]
    public void ValidRequest_NoGroupSize_Passes() =>
        Assert.True(this.validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "555-123-4567", null)).IsValid);

    [Fact]
    public void ValidRequest_WithGroupSize_Passes() =>
        Assert.True(this.validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "555-123-4567", 3)).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidGroupSize_Fails(int groupSize)
    {
        var result = this.validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "555-123-4567", groupSize));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "GroupSize");
    }

    [Fact]
    public void EmptyFirstName_Fails() =>
        Assert.False(this.validator.Validate(new AddGolferToWaitlistRequest("", "Smith", "555-123-4567", null)).IsValid);

    [Fact]
    public void EmptyLastName_Fails() =>
        Assert.False(this.validator.Validate(new AddGolferToWaitlistRequest("John", "", "555-123-4567", null)).IsValid);

    [Fact]
    public void InvalidPhone_Fails() =>
        Assert.False(this.validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "123", null)).IsValid);
}
