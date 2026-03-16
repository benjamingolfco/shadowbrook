using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Events;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;

namespace Shadowbrook.Domain.WalkUpWaitlistAggregate;

public class WalkUpWaitlist : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public string ShortCode { get; private set; } = string.Empty;
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private WalkUpWaitlist() { } // EF

    public static async Task<WalkUpWaitlist> OpenAsync(
        Guid courseId, DateOnly date, IShortCodeGenerator shortCodeGenerator,
        IWalkUpWaitlistRepository repository)
    {
        var existing = await repository.GetByCourseDateAsync(courseId, date);
        if (existing is not null)
        {
            throw new WaitlistAlreadyExistsException(existing.Status);
        }

        var shortCode = await shortCodeGenerator.GenerateAsync(date);
        var now = DateTimeOffset.UtcNow;
        return new WalkUpWaitlist
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            ShortCode = shortCode,
            Status = WaitlistStatus.Open,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Close()
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var now = DateTimeOffset.UtcNow;
        Status = WaitlistStatus.Closed;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public GolferWaitlistEntry AddGolfer(Golfer golfer, int groupSize = 1)
    {
        if (Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var entry = new GolferWaitlistEntry(Id, golfer.Id, groupSize);

        AddDomainEvent(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = Id,
            GolferId = golfer.Id,
            GolferName = golfer.FullName,
            GolferPhone = golfer.Phone,
            CourseId = CourseId,
            Position = 0
        });

        UpdatedAt = DateTimeOffset.UtcNow;
        return entry;
    }
}
