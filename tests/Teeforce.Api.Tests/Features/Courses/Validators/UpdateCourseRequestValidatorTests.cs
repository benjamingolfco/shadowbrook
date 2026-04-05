using FluentValidation.TestHelper;
using Teeforce.Api.Features.Courses;
using static Teeforce.Api.Features.Courses.CourseEndpoints;

namespace Teeforce.Api.Tests.Features.Courses.Validators;

public class UpdateCourseRequestValidatorTests
{
    private readonly UpdateCourseRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        this.validator.TestValidate(new UpdateCourseRequest("Course Name", "America/Chicago"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_Name_Fails() =>
        this.validator.TestValidate(new UpdateCourseRequest("", "America/Chicago"))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Missing_TimeZoneId_Fails() =>
        this.validator.TestValidate(new UpdateCourseRequest("Course Name", ""))
            .ShouldHaveValidationErrorFor(x => x.TimeZoneId);

    [Fact]
    public void Invalid_TimeZoneId_Fails() =>
        this.validator.TestValidate(new UpdateCourseRequest("Course Name", "Invalid/Timezone"))
            .ShouldHaveValidationErrorFor(x => x.TimeZoneId)
            .WithErrorMessage("TimeZoneId is not a valid IANA timezone.");
}
