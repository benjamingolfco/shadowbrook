using FluentValidation.TestHelper;
using Shadowbrook.Api.Features.Auth;

namespace Shadowbrook.Api.Tests.Features.Auth.Validators;

public class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator validator = new();

    [Fact]
    public void ValidRoleChangeToAdmin_Passes() =>
        this.validator.TestValidate(new UpdateUserRequest(null, "Admin", null))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ValidRoleChangeToOperatorWithOrganizationId_Passes() =>
        this.validator.TestValidate(new UpdateUserRequest(null, "Operator", Guid.CreateVersion7()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void InvalidRole_Fails() =>
        this.validator.TestValidate(new UpdateUserRequest(null, "SuperUser", null))
            .ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Invalid role. Must be Admin or Operator.");

    [Fact]
    public void OperatorRoleWithoutOrganizationId_Fails() =>
        this.validator.TestValidate(new UpdateUserRequest(null, "Operator", null))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("OrganizationId is required for Operator role.");

    [Fact]
    public void AdminRoleWithOrganizationId_Fails() =>
        this.validator.TestValidate(new UpdateUserRequest(null, "Admin", Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId)
            .WithErrorMessage("Admin users must not have an OrganizationId.");

    [Fact]
    public void OperatorRoleWithEmptyGuid_Fails() =>
        this.validator.TestValidate(new UpdateUserRequest(null, "Operator", Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId);
}
