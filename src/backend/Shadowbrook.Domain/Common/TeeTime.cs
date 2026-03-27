namespace Shadowbrook.Domain.Common;

public class TeeTime : IEquatable<TeeTime>
{
    public DateTime Value { get; }
    public DateOnly Date => DateOnly.FromDateTime(Value);
    public TimeOnly Time => TimeOnly.FromDateTime(Value);

    public TeeTime(DateOnly date, TimeOnly time)
    {
        Value = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0);
    }

    public TeeTime(DateTime value)
    {
        Value = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
    }

    private TeeTime() { } // EF

    public override bool Equals(object? obj) => Equals(obj as TeeTime);
    public bool Equals(TeeTime? other) => other is not null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Date:yyyy-MM-dd} {Time:h:mm tt}";
}
