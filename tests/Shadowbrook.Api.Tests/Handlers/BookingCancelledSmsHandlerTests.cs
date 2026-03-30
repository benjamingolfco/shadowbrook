using Microsoft.Extensions.Logging;
using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class BookingCancelledSmsHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();
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
    public async Task Handle_GolferNotFound_NoSmsAndLogsWarning()
    {
        var booking = BuildCancelledBooking();
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.golferRepo.GetByIdAsync(booking.GolferId).Returns((Golfer?)null);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_CourseNotFound_NoSmsAndLogsWarning()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var booking = BuildCancelledBooking(golferId: golfer.Id);
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);
        this.courseRepo.GetByIdAsync(booking.CourseId).Returns((Course?)null);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Success_SendsCancellationSms()
    {
        var golfer = Golfer.Create("+15559876543", "Bob", "Green");
        var course = Course.Create(Guid.NewGuid(), "Shadowbrook Golf Club", "America/Chicago");
        var booking = BuildCancelledBooking(golferId: golfer.Id, courseId: course.Id);
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);
        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.Received(1).SendAsync(
            "+15559876543",
            Arg.Is<string>(m => m.Contains("Shadowbrook Golf Club") && m.Contains("cancelled") && m.Contains("July 4, 2026") && m.Contains("8:00 AM")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingBookingCancelled_NoSmsAndLogsWarning()
    {
        var evt = BuildEvent(previousStatus: BookingStatus.Pending);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.golferRepo, this.courseRepo, this.sms, this.logger, CancellationToken.None);

        await this.sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.bookingRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
