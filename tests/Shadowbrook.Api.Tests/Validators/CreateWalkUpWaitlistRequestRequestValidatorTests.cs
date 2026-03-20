using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateWalkUpWaitlistRequestRequestValidatorTests
{
    private readonly CreateWalkUpWaitlistRequestRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", "09:00", 2)).IsValid);

    [Fact]
    public void ValidRequest_WithSeconds_Passes() =>
        Assert.True(validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", "09:00:00", 2)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("06/15/2026")]
    [InlineData("2026-13-01")]
    [InlineData("not-a-date")]
    public void InvalidDate_Fails(string date)
    {
        var result = validator.Validate(new CreateWalkUpWaitlistRequestRequest(date, "09:00", 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Date");
    }

    [Theory]
    [InlineData("")]
    [InlineData("9am")]
    [InlineData("25:00")]
    public void InvalidTeeTime_Fails(string teeTime)
    {
        var result = validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", teeTime, 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TeeTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidGolfersNeeded_Fails(int golfersNeeded)
    {
        var result = validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", "09:00", golfersNeeded));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "GolfersNeeded");
    }
}
