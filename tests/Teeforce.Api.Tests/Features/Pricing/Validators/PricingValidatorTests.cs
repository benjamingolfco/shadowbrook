using Teeforce.Api.Features.Pricing.Endpoints;

namespace Teeforce.Api.Tests.Features.Pricing.Validators;

public class CreateScheduleRequestValidatorTests
{
    private readonly CreateScheduleRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        Assert.True(this.validator.Validate(request).IsValid);
    }

    [Fact]
    public void Empty_Name_Fails()
    {
        var request = new CreateScheduleRequest("", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Empty_DaysOfWeek_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DaysOfWeek");
    }

    [Fact]
    public void StartTime_After_EndTime_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(14, 0), new TimeOnly(8, 0), 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "StartTime");
    }

    [Fact]
    public void Zero_Price_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 0m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Price");
    }

    [Fact]
    public void Negative_Price_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), -10m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Price");
    }
}

public class UpdateBoundsRequestValidatorTests
{
    private readonly UpdateBoundsRequestValidator validator = new();

    [Fact]
    public void Valid_BothNull_Passes()
    {
        var request = new UpdateBoundsRequest(null, null);
        Assert.True(this.validator.Validate(request).IsValid);
    }

    [Fact]
    public void Valid_BothSet_Passes()
    {
        var request = new UpdateBoundsRequest(10m, 100m);
        Assert.True(this.validator.Validate(request).IsValid);
    }

    [Fact]
    public void Negative_MinPrice_Fails()
    {
        var request = new UpdateBoundsRequest(-5m, 100m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MinPrice");
    }

    [Fact]
    public void Min_Greater_Than_Max_Fails()
    {
        var request = new UpdateBoundsRequest(100m, 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
    }
}
