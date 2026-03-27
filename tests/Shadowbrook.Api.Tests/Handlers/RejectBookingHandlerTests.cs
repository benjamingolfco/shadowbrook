using NSubstitute;
using Shadowbrook.Api.Features.Bookings.Handlers;
using Shadowbrook.Api.Features.Bookings.Policies;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.BookingAggregate.Exceptions;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Tests.Handlers;

public class RejectBookingHandlerTests
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
            playerCount: 2);
    }

    [Fact]
    public async Task Handle_PendingBooking_RejectsIt()
    {
        var booking = CreatePendingBooking();
        booking.ClearDomainEvents();
        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);

        var command = new RejectBookingCommand(booking.Id);

        await RejectBookingHandler.Handle(command, this.bookingRepo);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Contains(booking.DomainEvents, e => e is BookingRejected);
    }

    [Fact]
    public async Task Handle_BookingNotFound_Throws()
    {
        var bookingId = Guid.NewGuid();
        this.bookingRepo.GetByIdAsync(bookingId).Returns((Booking?)null);

        var command = new RejectBookingCommand(bookingId);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => RejectBookingHandler.Handle(command, this.bookingRepo));
    }

    [Fact]
    public async Task Handle_AlreadyConfirmedBooking_Throws()
    {
        var booking = CreatePendingBooking();
        booking.Confirm(); // move to Confirmed — no longer Pending
        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);

        var command = new RejectBookingCommand(booking.Id);

        await Assert.ThrowsAsync<BookingNotPendingException>(
            () => RejectBookingHandler.Handle(command, this.bookingRepo));
    }

    [Fact]
    public async Task Handle_AlreadyRejectedBooking_IsIdempotent()
    {
        var booking = CreatePendingBooking();
        booking.Reject(); // move to Rejected
        booking.ClearDomainEvents();
        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);

        var command = new RejectBookingCommand(booking.Id);

        await RejectBookingHandler.Handle(command, this.bookingRepo);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Empty(booking.DomainEvents);
    }
}
