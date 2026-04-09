namespace Teeforce.Domain.Common;

public class BookingDateTime : IEquatable<BookingDateTime>
{
    public DateTime Value { get; }
    public DateOnly Date => DateOnly.FromDateTime(Value);
    public TimeOnly Time => TimeOnly.FromDateTime(Value);

    public BookingDateTime(DateOnly date, TimeOnly time)
    {
        Value = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0);
    }

    public BookingDateTime(DateTime value)
    {
        Value = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
    }

    private BookingDateTime() { } // EF

    public override bool Equals(object? obj) => Equals(obj as BookingDateTime);
    public bool Equals(BookingDateTime? other) => other is not null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Date:yyyy-MM-dd} {Time:h:mm tt}";
}
