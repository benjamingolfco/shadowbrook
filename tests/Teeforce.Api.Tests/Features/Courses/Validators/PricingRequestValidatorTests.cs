using FluentValidation.TestHelper;
using Teeforce.Api.Features.Courses;
using static Teeforce.Api.Features.Courses.CourseEndpoints;

namespace Teeforce.Api.Tests.Features.Courses.Validators;

public class PricingRequestValidatorTests
{
    private readonly PricingRequestValidator validator = new();

    [Fact]
    public void Valid_Price_Passes() =>
        this.validator.TestValidate(new PricingRequest(45.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Zero_Price_Passes() =>
        this.validator.TestValidate(new PricingRequest(0.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Max_Price_Passes() =>
        this.validator.TestValidate(new PricingRequest(10000.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Negative_Price_Fails() =>
        this.validator.TestValidate(new PricingRequest(-10.00m))
            .ShouldHaveValidationErrorFor(x => x.FlatRatePrice);

    [Fact]
    public void Excessive_Price_Fails() =>
        this.validator.TestValidate(new PricingRequest(10001.00m))
            .ShouldHaveValidationErrorFor(x => x.FlatRatePrice);
}
