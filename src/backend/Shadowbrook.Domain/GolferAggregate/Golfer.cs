using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.GolferAggregate;

public class Golfer : Entity
{
    public string Phone { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    private Golfer() { } // EF

    public static Golfer Create(string phone, string firstName, string lastName)
    {
        var now = DateTimeOffset.UtcNow;
        return new Golfer
        {
            Id = Guid.CreateVersion7(),
            Phone = phone,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
