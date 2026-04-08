using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeSheetAggregate;

public class TeeSheetTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeSheetTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private static ScheduleSettings DefaultSettings() =>
        new(new TimeOnly(7, 0), new TimeOnly(9, 0), 30, 4);

    [Fact]
    public void Draft_EnumeratesIntervalsAtCorrectTimes()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);

        // 7:00, 7:30, 8:00, 8:30 — last (9:00) is exclusive
        Assert.Equal(4, sheet.Intervals.Count);
        Assert.Equal(new TimeOnly(7, 0), sheet.Intervals[0].Time);
        Assert.Equal(new TimeOnly(7, 30), sheet.Intervals[1].Time);
        Assert.Equal(new TimeOnly(8, 0), sheet.Intervals[2].Time);
        Assert.Equal(new TimeOnly(8, 30), sheet.Intervals[3].Time);
    }

    [Fact]
    public void Draft_AssignsCapacityFromSettings()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1),
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 2),
            this.timeProvider);

        Assert.All(sheet.Intervals, i => Assert.Equal(2, i.Capacity));
    }

    [Fact]
    public void Draft_AssignsUniqueIdsToIntervals()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);

        var ids = sheet.Intervals.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    [Fact]
    public void Draft_RaisesTeeSheetDraftedEvent()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);
        var sheet = TeeSheet.Draft(courseId, date, DefaultSettings(), this.timeProvider);

        var evt = Assert.IsType<TeeSheetDrafted>(Assert.Single(sheet.DomainEvents));
        Assert.Equal(sheet.Id, evt.TeeSheetId);
        Assert.Equal(courseId, evt.CourseId);
        Assert.Equal(date, evt.Date);
        Assert.Equal(4, evt.IntervalCount);
    }

    [Fact]
    public void Draft_StartsInDraftStatus()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        Assert.Equal(TeeSheetStatus.Draft, sheet.Status);
        Assert.Null(sheet.PublishedAt);
    }

    [Fact]
    public void Publish_TransitionsAndRaisesEvent()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        sheet.ClearDomainEvents();

        sheet.Publish(this.timeProvider);

        Assert.Equal(TeeSheetStatus.Published, sheet.Status);
        Assert.NotNull(sheet.PublishedAt);
        var evt = Assert.IsType<TeeSheetPublished>(Assert.Single(sheet.DomainEvents));
        Assert.Equal(sheet.Id, evt.TeeSheetId);
        Assert.Equal(sheet.PublishedAt.Value, evt.PublishedAt);
    }

    [Fact]
    public void Publish_IsIdempotent()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        sheet.Publish(this.timeProvider);
        sheet.ClearDomainEvents();

        sheet.Publish(this.timeProvider);

        Assert.Empty(sheet.DomainEvents);
    }

    [Fact]
    public void AuthorizeBooking_OnPublished_ReturnsTokenWithSheetId()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        sheet.Publish(this.timeProvider);

        var token = sheet.AuthorizeBooking();

        Assert.Equal(sheet.Id, token.SheetId);
    }

    [Fact]
    public void AuthorizeBooking_OnDraft_Throws()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);

        Assert.Throws<TeeSheetNotPublishedException>(() => sheet.AuthorizeBooking());
    }
}
