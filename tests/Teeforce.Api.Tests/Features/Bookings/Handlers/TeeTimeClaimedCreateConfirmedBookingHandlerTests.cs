using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;

namespace Teeforce.Api.Tests.Features.Bookings.Handlers;

public class TeeTimeClaimedCreateConfirmedBookingHandlerTests
{
    [Fact]
    public void Handle_AddsConfirmedBookingWithTeeTimeId()
    {
        var bookingRepo = Substitute.For<IBookingRepository>();
        var evt = new TeeTimeClaimed
        {
            TeeTimeId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 2,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            Time = new TimeOnly(9, 0),
        };

        TeeTimeClaimedCreateConfirmedBookingHandler.Handle(evt, bookingRepo);

        bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.Id == evt.BookingId
            && b.CourseId == evt.CourseId
            && b.GolferId == evt.GolferId
            && b.TeeTimeId == evt.TeeTimeId
            && b.PlayerCount == 2
            && b.Status == BookingStatus.Confirmed));
    }
}
