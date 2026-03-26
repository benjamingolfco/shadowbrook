namespace Shadowbrook.Domain.Common;

public class TeeTime : IEquatable<TeeTime>
{
    public DateOnly Date { get; }
    public TimeOnly Time { get; }

    public TeeTime(DateOnly date, TimeOnly time)
    {
        Date = date;
        Time = time;
    }

    private TeeTime() { } // EF

    public override bool Equals(object? obj) => Equals(obj as TeeTime);
    public bool Equals(TeeTime? other) => other is not null && Date == other.Date && Time == other.Time;
    public override int GetHashCode() => HashCode.Combine(Date, Time);
    public override string ToString() => $"{Date:yyyy-MM-dd} {Time:h:mm tt}";
}
