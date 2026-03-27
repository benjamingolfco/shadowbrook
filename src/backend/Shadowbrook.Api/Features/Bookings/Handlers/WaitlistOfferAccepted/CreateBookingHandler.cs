using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class WaitlistOfferAcceptedCreateBookingHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        IBookingRepository bookingRepository)
    {
        var booking = Booking.CreateConfirmed(
            bookingId: evt.BookingId,
            courseId: evt.CourseId,
            golferId: evt.GolferId,
            date: evt.Date,
            teeTime: evt.TeeTime,
            playerCount: evt.GroupSize);

        bookingRepository.Add(booking);

        await Task.CompletedTask;
    }
}
