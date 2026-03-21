using FluentValidation.TestHelper;
using Shadowbrook.Api.Features.Courses;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class TeeTimeSettingsRequestValidatorTests
{
    private readonly TeeTimeSettingsRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00")))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_Interval_Fails(int interval) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(interval, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00")))
            .ShouldHaveValidationErrorFor(x => x.TeeTimeIntervalMinutes);

    [Fact]
    public void FirstTeeTime_After_LastTeeTime_Fails() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("18:00"), TimeOnly.Parse("07:00")))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);

    [Fact]
    public void FirstTeeTime_Equals_LastTeeTime_Fails() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("07:00")))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);
}
