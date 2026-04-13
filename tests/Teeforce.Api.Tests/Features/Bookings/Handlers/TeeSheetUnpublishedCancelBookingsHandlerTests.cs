using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate;
using DomainScheduleSettings = Teeforce.Domain.TeeSheetAggregate.ScheduleSettings;
using DomainTeeSheet = Teeforce.Domain.TeeSheetAggregate.TeeSheet;

namespace Teeforce.Api.Tests.Features.Bookings.Handlers;

public class TeeSheetUnpublishedCancelBookingsHandlerTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly ITeeTimeRepository teeTimeRepository = Substitute.For<ITeeTimeRepository>();
    private readonly IBookingRepository bookingRepository = Substitute.For<IBookingRepository>();

    public TeeSheetUnpublishedCancelBookingsHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private TeeSheetUnpublished MakeEvent(Guid teeSheetId, Guid courseId, string? reason = null) =>
        new()
        {
            TeeSheetId = teeSheetId,
            CourseId = courseId,
            Date = new DateOnly(2026, 6, 1),
            Reason = reason,
            UnpublishedAt = this.timeProvider.GetCurrentTimestamp(),
        };

    private TeeTime MakeTeeTimeWithClaim(Guid courseId, Guid bookingId)
    {
        var settings = new DomainScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 4);
        var sheet = DomainTeeSheet.Draft(courseId, new DateOnly(2026, 6, 1), settings, this.timeProvider);
        sheet.Publish(this.timeProvider);
        var auth = sheet.AuthorizeBooking();
        return TeeTime.Claim(sheet.Intervals[0], courseId, sheet.Date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
    }

    [Fact]
    public async Task Handle_CancelsConfirmedBookingsWithReason()
    {
        var teeSheetId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var bookingId1 = Guid.CreateVersion7();
        var bookingId2 = Guid.CreateVersion7();
        var teeTime = MakeTeeTimeWithClaim(courseId, bookingId1);

        var booking1 = Booking.CreateConfirmed(bookingId1, courseId, Guid.NewGuid(), teeTime.Id, new DateOnly(2026, 6, 1), new TimeOnly(7, 0), 2);
        var booking2 = Booking.CreateConfirmed(bookingId2, courseId, Guid.NewGuid(), teeTime.Id, new DateOnly(2026, 6, 1), new TimeOnly(7, 0), 1);

        this.teeTimeRepository.GetByTeeSheetIdAsync(teeSheetId, Arg.Any<CancellationToken>())
            .Returns([teeTime]);
        this.bookingRepository.GetByTeeTimeIdsAsync(Arg.Is<List<Guid>>(ids => ids.Contains(teeTime.Id)), Arg.Any<CancellationToken>())
            .Returns([booking1, booking2]);

        var handler = new TeeSheetUnpublishedCancelBookingsHandler(
            this.teeTimeRepository, this.bookingRepository, NullLogger<TeeSheetUnpublishedCancelBookingsHandler>.Instance);
        await handler.Handle(MakeEvent(teeSheetId, courseId, "Course maintenance"), CancellationToken.None);

        Assert.Equal(BookingStatus.Cancelled, booking1.Status);
        Assert.Equal(BookingStatus.Cancelled, booking2.Status);
        var cancelEvt = booking1.DomainEvents.OfType<BookingCancelled>().Last();
        Assert.Equal("Course maintenance", cancelEvt.Reason);
    }

    [Fact]
    public async Task Handle_NoTeeTimes_SkipsBookingLookup()
    {
        var teeSheetId = Guid.NewGuid();
        this.teeTimeRepository.GetByTeeSheetIdAsync(teeSheetId, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new TeeSheetUnpublishedCancelBookingsHandler(
            this.teeTimeRepository, this.bookingRepository, NullLogger<TeeSheetUnpublishedCancelBookingsHandler>.Instance);
        await handler.Handle(MakeEvent(teeSheetId, Guid.NewGuid()), CancellationToken.None);

        await this.bookingRepository.DidNotReceive().GetByTeeTimeIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyCancelledBookings_AreIdempotent()
    {
        var teeSheetId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var bookingId = Guid.CreateVersion7();
        var teeTime = MakeTeeTimeWithClaim(courseId, bookingId);

        var booking = Booking.CreateConfirmed(bookingId, courseId, Guid.NewGuid(), teeTime.Id, new DateOnly(2026, 6, 1), new TimeOnly(7, 0), 2);
        booking.Cancel();
        var eventsBefore = booking.DomainEvents.Count;

        this.teeTimeRepository.GetByTeeSheetIdAsync(teeSheetId, Arg.Any<CancellationToken>())
            .Returns([teeTime]);
        this.bookingRepository.GetByTeeTimeIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([booking]);

        var handler = new TeeSheetUnpublishedCancelBookingsHandler(
            this.teeTimeRepository, this.bookingRepository, NullLogger<TeeSheetUnpublishedCancelBookingsHandler>.Instance);
        await handler.Handle(MakeEvent(teeSheetId, courseId), CancellationToken.None);

        Assert.Equal(eventsBefore, booking.DomainEvents.Count);
    }
}
