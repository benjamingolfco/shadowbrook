using FluentValidation.TestHelper;
using Teeforce.Api.Features.Courses;
using static Teeforce.Api.Features.Courses.CourseEndpoints;

namespace Teeforce.Api.Tests.Features.TeeSheet.Validators;

public class TeeTimeSettingsRequestValidatorTests
{
    private readonly TeeTimeSettingsRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), 4))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    public void Positive_Interval_Passes(int interval) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(interval, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), 4))
            .ShouldNotHaveValidationErrorFor(x => x.TeeTimeIntervalMinutes);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_Interval_Fails(int interval) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(interval, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), 4))
            .ShouldHaveValidationErrorFor(x => x.TeeTimeIntervalMinutes);

    [Fact]
    public void FirstTeeTime_After_LastTeeTime_Fails() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("18:00"), TimeOnly.Parse("07:00"), 4))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);

    [Fact]
    public void FirstTeeTime_Equals_LastTeeTime_Fails() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("07:00"), 4))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void Positive_DefaultCapacity_Passes(int capacity) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), capacity))
            .ShouldNotHaveValidationErrorFor(x => x.DefaultCapacity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_DefaultCapacity_Fails(int capacity) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), capacity))
            .ShouldHaveValidationErrorFor(x => x.DefaultCapacity);
}
