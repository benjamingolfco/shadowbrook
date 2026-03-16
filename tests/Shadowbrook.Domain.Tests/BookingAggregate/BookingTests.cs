using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;

namespace Shadowbrook.Domain.Tests.BookingAggregate;

public class BookingTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var bookingId = Guid.CreateVersion7();
        var courseId = Guid.NewGuid();
        var golferId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(9, 0);
        var golferName = "Jane Smith";
        var playerCount = 2;

        var booking = Booking.Create(bookingId, courseId, golferId, date, time, golferName, playerCount);

        Assert.Equal(bookingId, booking.Id);
        Assert.Equal(courseId, booking.CourseId);
        Assert.Equal(golferId, booking.GolferId);
        Assert.Equal(date, booking.Date);
        Assert.Equal(time, booking.Time);
        Assert.Equal(golferName, booking.GolferName);
        Assert.Equal(playerCount, booking.PlayerCount);
        Assert.NotEqual(default, booking.CreatedAt);
        Assert.NotEqual(default, booking.UpdatedAt);
    }

    [Fact]
    public void Create_UsesPreAllocatedBookingId()
    {
        var bookingId = Guid.CreateVersion7();

        var booking = Booking.Create(
            bookingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            "Jane Smith",
            1);

        Assert.Equal(bookingId, booking.Id);
    }

    [Fact]
    public void Create_RaisesBookingCreatedEvent()
    {
        var bookingId = Guid.CreateVersion7();
        var courseId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var booking = Booking.Create(
            bookingId,
            courseId,
            golferId,
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            "Jane Smith",
            1);

        var domainEvent = Assert.Single(booking.DomainEvents);
        var createdEvent = Assert.IsType<BookingCreated>(domainEvent);
        Assert.Equal(bookingId, createdEvent.BookingId);
        Assert.Equal(golferId, createdEvent.GolferId);
        Assert.Equal(courseId, createdEvent.CourseId);
    }

    [Fact]
    public void Create_EventCarriesIdentifiersOnly()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 0),
            "Jane Smith",
            1);

        var domainEvent = Assert.Single(booking.DomainEvents);
        var createdEvent = Assert.IsType<BookingCreated>(domainEvent);

        // BookingCreated carries only identifiers (BookingId, GolferId, CourseId) plus IDomainEvent metadata
        Assert.NotEqual(Guid.Empty, createdEvent.BookingId);
        Assert.NotEqual(Guid.Empty, createdEvent.GolferId);
        Assert.NotEqual(Guid.Empty, createdEvent.CourseId);
        Assert.NotEqual(Guid.Empty, createdEvent.EventId);
        Assert.NotEqual(default, createdEvent.OccurredAt);
    }
}
