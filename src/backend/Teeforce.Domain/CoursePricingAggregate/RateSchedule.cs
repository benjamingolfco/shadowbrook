using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate;

public class RateSchedule : Entity
{
    public Guid CoursePricingSettingsId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DayOfWeek[] DaysOfWeek { get; private set; } = [];
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public decimal Price { get; private set; }

    private RateSchedule() { } // EF

    internal RateSchedule(Guid parentId, string name, DayOfWeek[] daysOfWeek, TimeOnly startTime, TimeOnly endTime, decimal price)
    {
        Id = Guid.CreateVersion7();
        CoursePricingSettingsId = parentId;
        Name = name;
        DaysOfWeek = daysOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Price = price;
    }

    internal void Update(string name, DayOfWeek[] daysOfWeek, TimeOnly startTime, TimeOnly endTime, decimal price)
    {
        Name = name;
        DaysOfWeek = daysOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Price = price;
    }

    internal TimeSpan TimeBandWidth => EndTime.ToTimeSpan() - StartTime.ToTimeSpan();

    internal bool OverlapsTimeWith(RateSchedule other) =>
        StartTime < other.EndTime && other.StartTime < EndTime;

    internal bool SharesDayWith(RateSchedule other) =>
        DaysOfWeek.Any(d => other.DaysOfWeek.Contains(d));

    internal bool HasSameSpecificityAs(RateSchedule other) =>
        DaysOfWeek.Length == other.DaysOfWeek.Length && TimeBandWidth == other.TimeBandWidth;
}
