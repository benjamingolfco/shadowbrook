using FluentValidation.TestHelper;
using Teeforce.Api.Features.Organizations;

namespace Teeforce.Api.Tests.Features.Organizations.Validators;

public class CreateOrganizationRequestValidatorTests
{
    private readonly CreateOrganizationRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        this.validator.TestValidate(new CreateOrganizationRequest("Pebble Beach", "operator@example.com"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyName_Fails() =>
        this.validator.TestValidate(new CreateOrganizationRequest("", "operator@example.com"))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void EmptyEmail_Fails() =>
        this.validator.TestValidate(new CreateOrganizationRequest("Pebble Beach", ""))
            .ShouldHaveValidationErrorFor(x => x.OperatorEmail);

    [Fact]
    public void InvalidEmail_Fails() =>
        this.validator.TestValidate(new CreateOrganizationRequest("Pebble Beach", "not-an-email"))
            .ShouldHaveValidationErrorFor(x => x.OperatorEmail);

    [Fact]
    public void NameTooLong_Fails() =>
        this.validator.TestValidate(new CreateOrganizationRequest(new string('A', 201), "operator@example.com"))
            .ShouldHaveValidationErrorFor(x => x.Name);
}
