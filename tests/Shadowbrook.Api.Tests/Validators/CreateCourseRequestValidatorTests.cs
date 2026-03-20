using FluentValidation.TestHelper;
using Shadowbrook.Api.Features.Courses;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateCourseRequestValidatorTests
{
    private readonly CreateCourseRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        validator.TestValidate(new CreateCourseRequest("Course Name"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_Name_Fails() =>
        validator.TestValidate(new CreateCourseRequest(""))
            .ShouldHaveValidationErrorFor(x => x.Name);
}
