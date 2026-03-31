using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.CourseAggregate;

public class Course : Entity
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? StreetAddress { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? ZipCode { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public int? TeeTimeIntervalMinutes { get; private set; }
    public TimeOnly? FirstTeeTime { get; private set; }
    public TimeOnly? LastTeeTime { get; private set; }
    public decimal? FlatRatePrice { get; private set; }
    public Dictionary<string, bool>? FeatureFlags { get; private set; }
    public bool? WaitlistEnabled { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private Course() { } // EF

    public static Course Create(
        Guid organizationId,
        string name,
        string timeZoneId,
        string? streetAddress = null,
        string? city = null,
        string? state = null,
        string? zipCode = null,
        string? contactEmail = null,
        string? contactPhone = null)
    {
        return new Course
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            TimeZoneId = timeZoneId,
            StreetAddress = streetAddress,
            City = city,
            State = state,
            ZipCode = zipCode,
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateTeeTimeSettings(int intervalMinutes, TimeOnly firstTeeTime, TimeOnly lastTeeTime)
    {
        TeeTimeIntervalMinutes = intervalMinutes;
        FirstTeeTime = firstTeeTime;
        LastTeeTime = lastTeeTime;
    }

    public void UpdatePricing(decimal flatRatePrice) => FlatRatePrice = flatRatePrice;
}
