using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.BookingAggregate.Exceptions;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Tests.Handlers;

public class ConfirmBookingHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();

    private static Booking CreatePendingBooking()
    {
        return Booking.Create(
            bookingId: Guid.CreateVersion7(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            date: new DateOnly(2026, 3, 25),
            teeTime: new TimeOnly(10, 0),
            golferName: "Jane Smith",
            playerCount: 2);
    }

    [Fact]
    public async Task Handle_PendingBooking_ConfirmsIt()
    {
        var booking = CreatePendingBooking();
        booking.ClearDomainEvents();
        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);

        var command = new ConfirmBookingCommand(booking.Id);

        await ConfirmBookingHandler.Handle(command, this.bookingRepo);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Contains(booking.DomainEvents, e => e is BookingConfirmed);
    }

    [Fact]
    public async Task Handle_BookingNotFound_Throws()
    {
        var bookingId = Guid.NewGuid();
        this.bookingRepo.GetByIdAsync(bookingId).Returns((Booking?)null);

        var command = new ConfirmBookingCommand(bookingId);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => ConfirmBookingHandler.Handle(command, this.bookingRepo));
    }

    [Fact]
    public async Task Handle_AlreadyConfirmedBooking_Throws()
    {
        var booking = CreatePendingBooking();
        booking.Confirm(); // move to Confirmed
        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);

        var command = new ConfirmBookingCommand(booking.Id);

        await Assert.ThrowsAsync<BookingNotPendingException>(
            () => ConfirmBookingHandler.Handle(command, this.bookingRepo));
    }
}
