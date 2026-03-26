using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateOpeningRequestValidatorTests
{
    private readonly CreateOpeningRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(this.validator.Validate(new CreateOpeningRequest("09:00", 2)).IsValid);

    [Fact]
    public void ValidRequest_WithSeconds_Passes() =>
        Assert.True(this.validator.Validate(new CreateOpeningRequest("09:00:00", 2)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("9am")]
    [InlineData("25:00")]
    public void InvalidTeeTime_Fails(string teeTime)
    {
        var result = this.validator.Validate(new CreateOpeningRequest(teeTime, 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TeeTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidSlotsAvailable_Fails(int slots)
    {
        var result = this.validator.Validate(new CreateOpeningRequest("09:00", slots));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SlotsAvailable");
    }
}
