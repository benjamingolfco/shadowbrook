using Teeforce.Domain.TenantAggregate;

namespace Teeforce.Domain.Tests.TenantAggregate;

public class TenantTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenant = Tenant.Create("Benjamin Golf Co", "Aaron Benjamin", "aaron@benjamingolf.co", "+15551234567");

        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal("Benjamin Golf Co", tenant.OrganizationName);
        Assert.Equal("Aaron Benjamin", tenant.ContactName);
        Assert.Equal("aaron@benjamingolf.co", tenant.ContactEmail);
        Assert.Equal("+15551234567", tenant.ContactPhone);
        Assert.NotEqual(default, tenant.CreatedAt);
    }

    [Fact]
    public void Create_TrimsOrganizationName()
    {
        var tenant = Tenant.Create("  Benjamin Golf Co  ", "Aaron Benjamin", "aaron@benjamingolf.co", "+15551234567");

        Assert.Equal("Benjamin Golf Co", tenant.OrganizationName);
    }

    [Fact]
    public void Create_TrimsContactName()
    {
        var tenant = Tenant.Create("Benjamin Golf Co", "  Aaron Benjamin  ", "aaron@benjamingolf.co", "+15551234567");

        Assert.Equal("Aaron Benjamin", tenant.ContactName);
    }

    [Fact]
    public void Create_TrimsContactEmail()
    {
        var tenant = Tenant.Create("Benjamin Golf Co", "Aaron Benjamin", "  aaron@benjamingolf.co  ", "+15551234567");

        Assert.Equal("aaron@benjamingolf.co", tenant.ContactEmail);
    }

    [Fact]
    public void Create_TrimsContactPhone()
    {
        var tenant = Tenant.Create("Benjamin Golf Co", "Aaron Benjamin", "aaron@benjamingolf.co", "  +15551234567  ");

        Assert.Equal("+15551234567", tenant.ContactPhone);
    }
}
