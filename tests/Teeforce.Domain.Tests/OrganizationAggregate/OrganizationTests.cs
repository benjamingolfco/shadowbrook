using Teeforce.Domain.OrganizationAggregate;

namespace Teeforce.Domain.Tests.OrganizationAggregate;

public class OrganizationTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var org = Organization.Create("Benjamin Golf Co");

        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("Benjamin Golf Co", org.Name);
        Assert.True(org.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public void Create_TrimsName()
    {
        var org = Organization.Create("  Benjamin Golf Co  ");

        Assert.Equal("Benjamin Golf Co", org.Name);
    }
}
