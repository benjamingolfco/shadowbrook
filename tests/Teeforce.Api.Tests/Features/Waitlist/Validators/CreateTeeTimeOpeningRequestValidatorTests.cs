using Teeforce.Api.Features.Waitlist.Endpoints;

namespace Teeforce.Api.Tests.Features.Waitlist.Validators;

public class CreateTeeTimeOpeningRequestValidatorTests
{
    private readonly CreateTeeTimeOpeningRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(this.validator.Validate(new CreateTeeTimeOpeningRequest(new DateTime(2026, 3, 30, 9, 0, 0), 2)).IsValid);

    [Fact]
    public void DefaultDateTime_Fails()
    {
        var result = this.validator.Validate(new CreateTeeTimeOpeningRequest(default, 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TeeTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidSlotsAvailable_Fails(int slots)
    {
        var result = this.validator.Validate(new CreateTeeTimeOpeningRequest(new DateTime(2026, 3, 30, 9, 0, 0), slots));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SlotsAvailable");
    }
}
