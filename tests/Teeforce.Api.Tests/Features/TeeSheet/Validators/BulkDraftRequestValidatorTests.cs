using FluentValidation.TestHelper;
using Teeforce.Api.Features.TeeSheet.Endpoints;

namespace Teeforce.Api.Tests.Features.TeeSheet.Validators;

public class BulkDraftRequestValidatorTests
{
    private readonly BulkDraftRequestValidator validator = new();

    [Fact]
    public void Valid_SingleDate_Passes() =>
        this.validator.TestValidate(new BulkDraftRequest([DateOnly.Parse("2026-04-13")]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Valid_MultipleDates_Passes() =>
        this.validator.TestValidate(new BulkDraftRequest([
            DateOnly.Parse("2026-04-13"),
            DateOnly.Parse("2026-04-14"),
            DateOnly.Parse("2026-04-15"),
        ]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Null_Dates_Fails() =>
        this.validator.TestValidate(new BulkDraftRequest(null!))
            .ShouldHaveValidationErrorFor(x => x.Dates);

    [Fact]
    public void Empty_Dates_Fails() =>
        this.validator.TestValidate(new BulkDraftRequest([]))
            .ShouldHaveValidationErrorFor(x => x.Dates);
}
