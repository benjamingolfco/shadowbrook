using FluentValidation.TestHelper;
using Shadowbrook.Api.Features.Courses;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class PricingRequestValidatorTests
{
    private readonly PricingRequestValidator validator = new();

    [Fact]
    public void Valid_Price_Passes() =>
        validator.TestValidate(new PricingRequest(45.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Zero_Price_Passes() =>
        validator.TestValidate(new PricingRequest(0.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Max_Price_Passes() =>
        validator.TestValidate(new PricingRequest(10000.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Negative_Price_Fails() =>
        validator.TestValidate(new PricingRequest(-10.00m))
            .ShouldHaveValidationErrorFor(x => x.FlatRatePrice);

    [Fact]
    public void Excessive_Price_Fails() =>
        validator.TestValidate(new PricingRequest(10001.00m))
            .ShouldHaveValidationErrorFor(x => x.FlatRatePrice);
}
