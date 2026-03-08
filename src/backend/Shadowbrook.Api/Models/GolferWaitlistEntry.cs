using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Events;

namespace Shadowbrook.Api.Models;

public class GolferWaitlistEntry : Entity
{
    private GolferWaitlistEntry() { } // EF

    public GolferWaitlistEntry(Guid id)
    {
        this.Id = id;
    }

    public required Guid CourseWaitlistId { get; set; }
    public required Guid GolferId { get; set; }
    public required string GolferName { get; set; }
    public required string GolferPhone { get; set; }
    public bool IsWalkUp { get; set; } = true;
    public bool IsReady { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Golfer? Golfer { get; set; }

    public void RaiseJoinedEvent(string courseName, int position) =>
        AddDomainEvent(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = this.Id,
            CourseWaitlistId = this.CourseWaitlistId,
            GolferId = this.GolferId,
            GolferName = this.GolferName,
            GolferPhone = this.GolferPhone,
            CourseName = courseName,
            Position = position
        });
}
