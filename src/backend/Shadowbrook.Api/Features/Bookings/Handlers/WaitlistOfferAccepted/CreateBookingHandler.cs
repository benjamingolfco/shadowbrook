using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Bookings.Handlers;

public static class WaitlistOfferAcceptedCreateBookingHandler
{
    public static async Task Handle(
        WaitlistOfferAccepted evt,
        IGolferRepository golferRepository,
        IBookingRepository bookingRepository)
    {
        var golfer = await golferRepository.GetRequiredByIdAsync(evt.GolferId);

        var booking = Booking.Create(
            bookingId: Guid.CreateVersion7(),
            courseId: evt.CourseId,
            golferId: evt.GolferId,
            date: evt.Date,
            teeTime: evt.TeeTime,
            golferName: golfer.FullName,
            playerCount: evt.GroupSize);

        bookingRepository.Add(booking);
    }
}
