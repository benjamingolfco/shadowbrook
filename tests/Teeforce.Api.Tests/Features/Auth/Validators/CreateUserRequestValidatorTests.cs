using FluentValidation.TestHelper;
using Teeforce.Api.Features.Auth;

namespace Teeforce.Api.Tests.Features.Auth.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator validator = new();

    [Fact]
    public void ValidAdmin_Passes() =>
        this.validator.TestValidate(new CreateUserRequest("admin@example.com", "Admin", null))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ValidOperator_Passes() =>
        this.validator.TestValidate(new CreateUserRequest("operator@example.com", "Operator", Guid.CreateVersion7()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyEmail_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("", "Admin", null))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void InvalidRole_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("admin@example.com", "SuperUser", null))
            .ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Invalid role. Must be Admin or Operator.");

    [Fact]
    public void OperatorWithoutOrganizationId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("op@example.com", "Operator", null))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("OrganizationId is required for Operator role.");

    [Fact]
    public void AdminWithOrganizationId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("admin@example.com", "Admin", Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("Admin users must not have an OrganizationId.");

    [Fact]
    public void OperatorWithEmptyGuid_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("op@example.com", "Operator", Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId);
}
