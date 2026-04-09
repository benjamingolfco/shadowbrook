using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.TeeSheetAggregate;

public class TeeSheet : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TeeSheetStatus Status { get; private set; }
    public ScheduleSettings Settings { get; private set; } = null!;
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeSheetInterval> intervals = [];
    public IReadOnlyList<TeeSheetInterval> Intervals => this.intervals.AsReadOnly();

    private TeeSheet() { } // EF

    public static TeeSheet Draft(
        Guid courseId,
        DateOnly date,
        ScheduleSettings settings,
        ITimeProvider timeProvider)
    {
        var sheet = new TeeSheet
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            Status = TeeSheetStatus.Draft,
            Settings = settings,
            CreatedAt = timeProvider.GetCurrentTimestamp(),
        };

        var current = settings.FirstTeeTime;
        var step = TimeSpan.FromMinutes(settings.IntervalMinutes);
        while (current < settings.LastTeeTime)
        {
            sheet.intervals.Add(new TeeSheetInterval(sheet.Id, current, settings.DefaultCapacity));
            current = current.Add(step);
        }

        sheet.AddDomainEvent(new TeeSheetDrafted
        {
            TeeSheetId = sheet.Id,
            CourseId = courseId,
            Date = date,
            IntervalCount = sheet.intervals.Count,
        });

        return sheet;
    }

    public void Publish(ITimeProvider timeProvider)
    {
        if (Status == TeeSheetStatus.Published)
        {
            return;
        }

        Status = TeeSheetStatus.Published;
        PublishedAt = timeProvider.GetCurrentTimestamp();

        AddDomainEvent(new TeeSheetPublished
        {
            TeeSheetId = Id,
            CourseId = CourseId,
            Date = Date,
            PublishedAt = PublishedAt.Value,
        });
    }

    public BookingAuthorization AuthorizeBooking()
    {
        if (Status != TeeSheetStatus.Published)
        {
            throw new TeeSheetNotPublishedException(Id);
        }

        return new BookingAuthorization(Id);
    }
}
