using FluentValidation.TestHelper;
using Teeforce.Api.Features.TeeSheet.Endpoints;

namespace Teeforce.Api.Tests.Features.TeeSheet.Validators;

public class WeeklyStatusRequestValidatorTests
{
    private readonly WeeklyStatusRequestValidator validator = new();

    [Fact]
    public void Valid_StartDate_Passes() =>
        this.validator.TestValidate(new WeeklyStatusRequest("2026-04-13"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_StartDate_Fails() =>
        this.validator.TestValidate(new WeeklyStatusRequest(null!))
            .ShouldHaveValidationErrorFor(x => x.StartDate);

    [Fact]
    public void Empty_StartDate_Fails() =>
        this.validator.TestValidate(new WeeklyStatusRequest(""))
            .ShouldHaveValidationErrorFor(x => x.StartDate);
}
