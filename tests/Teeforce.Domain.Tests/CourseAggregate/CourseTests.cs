using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Domain.Tests.CourseAggregate;

public class CourseTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public void Create_SetsAllRequiredProperties()
    {
        var course = Course.Create(OrganizationId, "Pebble Beach", "America/Los_Angeles");

        Assert.NotEqual(Guid.Empty, course.Id);
        Assert.Equal(OrganizationId, course.OrganizationId);
        Assert.Equal("Pebble Beach", course.Name);
        Assert.Equal("America/Los_Angeles", course.TimeZoneId);
        Assert.NotEqual(default, course.CreatedAt);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var course = Course.Create(OrganizationId, "  Pebble Beach  ", "America/Los_Angeles");

        Assert.Equal("Pebble Beach", course.Name);
    }

    [Fact]
    public void Create_WithOptionalParameters_SetsAllProperties()
    {
        var course = Course.Create(
            OrganizationId,
            "Augusta National",
            "America/New_York",
            streetAddress: "2604 Washington Rd",
            city: "Augusta",
            state: "GA",
            zipCode: "30904",
            contactEmail: "info@augusta.com",
            contactPhone: "+17065550100");

        Assert.Equal("2604 Washington Rd", course.StreetAddress);
        Assert.Equal("Augusta", course.City);
        Assert.Equal("GA", course.State);
        Assert.Equal("30904", course.ZipCode);
        Assert.Equal("info@augusta.com", course.ContactEmail);
        Assert.Equal("+17065550100", course.ContactPhone);
    }

    [Fact]
    public void Create_WithoutOptionalParameters_LeavesThemNull()
    {
        var course = Course.Create(OrganizationId, "Pebble Beach", "America/Los_Angeles");

        Assert.Null(course.StreetAddress);
        Assert.Null(course.City);
        Assert.Null(course.State);
        Assert.Null(course.ZipCode);
        Assert.Null(course.ContactEmail);
        Assert.Null(course.ContactPhone);
        Assert.Null(course.TeeTimeIntervalMinutes);
        Assert.Null(course.FirstTeeTime);
        Assert.Null(course.LastTeeTime);
        Assert.Null(course.FlatRatePrice);
        Assert.Null(course.WaitlistEnabled);
    }

    [Fact]
    public void UpdateTeeTimeSettings_SetsAllThreeProperties()
    {
        var course = Course.Create(OrganizationId, "Pebble Beach", "America/Los_Angeles");
        var first = new TimeOnly(7, 0);
        var last = new TimeOnly(17, 0);

        course.UpdateTeeTimeSettings(15, first, last);

        Assert.Equal(15, course.TeeTimeIntervalMinutes);
        Assert.Equal(first, course.FirstTeeTime);
        Assert.Equal(last, course.LastTeeTime);
    }

    [Fact]
    public void UpdatePricing_SetsFlatRatePrice()
    {
        var course = Course.Create(OrganizationId, "Pebble Beach", "America/Los_Angeles");

        course.UpdatePricing(59.99m);

        Assert.Equal(59.99m, course.FlatRatePrice);
    }
}
