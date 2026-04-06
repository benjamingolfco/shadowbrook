using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class BookingCancelledNotificationHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    private static BookingCancelled BuildEvent(Guid? bookingId = null, BookingStatus? previousStatus = null)
    {
        return new BookingCancelled
        {
            BookingId = bookingId ?? Guid.NewGuid(),
            PreviousStatus = previousStatus ?? BookingStatus.Confirmed
        };
    }

    private static Booking BuildCancelledBooking(Guid? bookingId = null, Guid? courseId = null, Guid? golferId = null)
    {
        var booking = Booking.CreateConfirmed(
            bookingId ?? Guid.CreateVersion7(),
            courseId ?? Guid.NewGuid(),
            golferId ?? Guid.NewGuid(),
            new DateOnly(2026, 7, 4),
            new TimeOnly(8, 0),
            2);
        booking.Cancel();
        booking.ClearDomainEvents();
        return booking;
    }

    [Fact]
    public async Task Handle_CourseNotFound_NoNotificationAndLogsWarning()
    {
        var booking = BuildCancelledBooking();
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.courseRepo.GetByIdAsync(booking.CourseId).Returns((Course?)null);

        await BookingCancelledNotificationHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<BookingCancellation>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Success_SendsCancellationNotification()
    {
        var golferId = Guid.CreateVersion7();
        var course = Course.Create(Guid.NewGuid(), "Teeforce Golf Club", "America/Chicago");
        var booking = BuildCancelledBooking(golferId: golferId, courseId: course.Id);
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await BookingCancelledNotificationHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<BookingCancellation>(n =>
                n.CourseName == "Teeforce Golf Club" &&
                n.Date == new DateOnly(2026, 7, 4) &&
                n.Time == new TimeOnly(8, 0)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingBookingCancelled_NoNotificationAndLogsWarning()
    {
        var evt = BuildEvent(previousStatus: BookingStatus.Pending);

        await BookingCancelledNotificationHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<BookingCancellation>(), Arg.Any<CancellationToken>());
        await this.bookingRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
