using FluentValidation.TestHelper;
using Shadowbrook.Api.Features.Courses;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateCourseRequestValidatorTests
{
    private readonly CreateCourseRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        this.validator.TestValidate(new CreateCourseRequest("Course Name", "America/Chicago"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_Name_Fails() =>
        this.validator.TestValidate(new CreateCourseRequest("", "America/Chicago"))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Missing_TimeZoneId_Fails() =>
        this.validator.TestValidate(new CreateCourseRequest("Course Name", ""))
            .ShouldHaveValidationErrorFor(x => x.TimeZoneId);

    [Fact]
    public void Invalid_TimeZoneId_Fails() =>
        this.validator.TestValidate(new CreateCourseRequest("Course Name", "Invalid/Timezone"))
            .ShouldHaveValidationErrorFor(x => x.TimeZoneId)
            .WithErrorMessage("TimeZoneId is not a valid IANA timezone.");
}
