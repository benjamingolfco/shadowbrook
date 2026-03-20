using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Domain.Tests.GolferAggregate;

public class GolferTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");

        Assert.Equal("+15551234567", golfer.Phone);
        Assert.Equal("Jane", golfer.FirstName);
        Assert.Equal("Smith", golfer.LastName);
        Assert.NotEqual(Guid.Empty, golfer.Id);
        Assert.NotEqual(default, golfer.CreatedAt);
    }

    [Fact]
    public void Create_TrimsFirstName()
    {
        var golfer = Golfer.Create("+15551234567", "  Jane  ", "Smith");
        Assert.Equal("Jane", golfer.FirstName);
    }

    [Fact]
    public void Create_TrimsLastName()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "  Smith  ");
        Assert.Equal("Smith", golfer.LastName);
    }

    [Fact]
    public void FullName_CombinesFirstAndLast()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        Assert.Equal("Jane Smith", golfer.FullName);
    }
}
