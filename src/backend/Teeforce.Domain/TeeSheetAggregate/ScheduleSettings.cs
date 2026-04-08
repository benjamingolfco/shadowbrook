using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.TeeSheetAggregate;

public class ScheduleSettings : IEquatable<ScheduleSettings>
{
    public TimeOnly FirstTeeTime { get; }
    public TimeOnly LastTeeTime { get; }
    public int IntervalMinutes { get; }
    public int DefaultCapacity { get; }

    public ScheduleSettings(TimeOnly firstTeeTime, TimeOnly lastTeeTime, int intervalMinutes, int defaultCapacity)
    {
        if (firstTeeTime >= lastTeeTime)
        {
            throw new InvalidScheduleSettingsException("First tee time must be earlier than last tee time.");
        }
        if (intervalMinutes <= 0)
        {
            throw new InvalidScheduleSettingsException("Interval minutes must be positive.");
        }
        if (defaultCapacity <= 0)
        {
            throw new InvalidScheduleSettingsException("Default capacity must be positive.");
        }

        FirstTeeTime = firstTeeTime;
        LastTeeTime = lastTeeTime;
        IntervalMinutes = intervalMinutes;
        DefaultCapacity = defaultCapacity;
    }

    private ScheduleSettings() { } // EF

    public override bool Equals(object? obj) => Equals(obj as ScheduleSettings);
    public bool Equals(ScheduleSettings? other) =>
        other is not null
        && FirstTeeTime == other.FirstTeeTime
        && LastTeeTime == other.LastTeeTime
        && IntervalMinutes == other.IntervalMinutes
        && DefaultCapacity == other.DefaultCapacity;

    public override int GetHashCode() =>
        HashCode.Combine(FirstTeeTime, LastTeeTime, IntervalMinutes, DefaultCapacity);
}
