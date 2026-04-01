using FluentValidation.TestHelper;
using Shadowbrook.Api.Features.Auth;

namespace Shadowbrook.Api.Tests.Features.Auth.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator validator = new();

    [Fact]
    public void ValidAdmin_Passes() =>
        this.validator.TestValidate(new CreateUserRequest("oid-123", "admin@example.com", "Admin User", "Admin", null))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ValidOperator_Passes() =>
        this.validator.TestValidate(new CreateUserRequest("oid-456", "operator@example.com", "Op User", "Operator", Guid.CreateVersion7()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyIdentityId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("", "admin@example.com", "Admin User", "Admin", null))
            .ShouldHaveValidationErrorFor(x => x.IdentityId);

    [Fact]
    public void EmptyEmail_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("oid-123", "", "Admin User", "Admin", null))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void InvalidRole_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("oid-123", "admin@example.com", "Admin User", "SuperUser", null))
            .ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Invalid role. Must be Admin or Operator.");

    [Fact]
    public void OperatorWithoutOrganizationId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("oid-456", "op@example.com", "Op User", "Operator", null))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("OrganizationId is required for Operator role.");

    [Fact]
    public void AdminWithOrganizationId_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("oid-123", "admin@example.com", "Admin User", "Admin", Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("Admin users must not have an OrganizationId.");

    [Fact]
    public void OperatorWithEmptyGuid_Fails() =>
        this.validator.TestValidate(new CreateUserRequest("oid-456", "op@example.com", "Op User", "Operator", Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId);
}
