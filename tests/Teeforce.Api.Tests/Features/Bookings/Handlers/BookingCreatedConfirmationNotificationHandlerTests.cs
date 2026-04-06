using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Tests.Features.Bookings.Handlers;

public class BookingCreatedConfirmationNotificationHandlerTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();

    public BookingCreatedConfirmationNotificationHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        this.db = new ApplicationDbContext(options, userContext: null);
    }

    public void Dispose() => this.db.Dispose();

    private static Booking BuildBooking(Guid? bookingId = null, Guid? courseId = null, Guid? golferId = null)
    {
        var booking = Booking.CreateConfirmed(
            bookingId ?? Guid.CreateVersion7(),
            courseId ?? Guid.NewGuid(),
            golferId ?? Guid.NewGuid(),
            new DateOnly(2026, 7, 4),
            new TimeOnly(9, 0),
            2);
        booking.ClearDomainEvents();
        return booking;
    }

    private static BookingCreated BuildEvent(Guid bookingId, Guid courseId, Guid golferId) =>
        new()
        {
            BookingId = bookingId,
            GolferId = golferId,
            CourseId = courseId,
            Date = new DateOnly(2026, 7, 4),
            TeeTime = new TimeOnly(9, 0),
            GroupSize = 2
        };

    [Fact]
    public async Task Handle_Success_SendsNotificationWithCourseNameAndTime()
    {
        var golferId = Guid.CreateVersion7();
        var course = Course.Create(Guid.NewGuid(), "Teeforce Golf Club", "America/Chicago");
        var booking = BuildBooking(courseId: course.Id, golferId: golferId);
        var evt = BuildEvent(booking.Id, course.Id, golferId);

        this.db.Courses.Add(course);
        await this.db.SaveChangesAsync();

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);

        await BookingCreatedConfirmationNotificationHandler.Handle(evt, this.bookingRepo, this.db, this.notificationService, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<BookingConfirmation>(n =>
                n.CourseName == "Teeforce Golf Club" &&
                n.Date == new DateOnly(2026, 7, 4) &&
                n.Time == new TimeOnly(9, 0)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CourseNotFound_ThrowsInvalidOperationException()
    {
        var courseId = Guid.NewGuid();
        var booking = BuildBooking(courseId: courseId);
        var evt = BuildEvent(booking.Id, courseId, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BookingCreatedConfirmationNotificationHandler.Handle(evt, this.bookingRepo, this.db, this.notificationService, CancellationToken.None));

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<BookingConfirmation>(), Arg.Any<CancellationToken>());
    }
}
