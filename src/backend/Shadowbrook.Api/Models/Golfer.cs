namespace Shadowbrook.Api.Models;

public class Golfer
{
    public Guid Id { get; set; }
    public required string Phone { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
