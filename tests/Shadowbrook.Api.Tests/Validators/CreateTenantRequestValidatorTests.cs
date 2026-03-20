using Shadowbrook.Api.Features.Tenants;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateTenantRequestValidatorTests
{
    private readonly CreateTenantRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        Assert.True(this.validator.Validate(new CreateTenantRequest("Org", "Name", "e@e.com", "555-123-4567")).IsValid);

    [Fact]
    public void Missing_OrganizationName_Fails()
    {
        var result = this.validator.Validate(new CreateTenantRequest("", "Name", "e@e.com", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OrganizationName");
    }

    [Fact]
    public void Missing_ContactName_Fails()
    {
        var result = this.validator.Validate(new CreateTenantRequest("Org", "", "e@e.com", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContactName");
    }

    [Fact]
    public void Missing_ContactEmail_Fails()
    {
        var result = this.validator.Validate(new CreateTenantRequest("Org", "Name", "", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void Invalid_ContactEmail_Fails()
    {
        var result = this.validator.Validate(new CreateTenantRequest("Org", "Name", "not-email", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void Missing_ContactPhone_Fails()
    {
        var result = this.validator.Validate(new CreateTenantRequest("Org", "Name", "e@e.com", ""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContactPhone");
    }
}
