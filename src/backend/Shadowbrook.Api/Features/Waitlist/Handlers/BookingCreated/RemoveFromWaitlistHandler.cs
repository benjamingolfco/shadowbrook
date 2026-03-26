using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Features.Waitlist.Handlers;

// DEPRECATED: Waitlist removal now happens on BookingConfirmed, not WaitlistOfferAccepted
// This ensures that golfers whose bookings are rejected (claim failed) remain on the waitlist
public static class WaitlistOfferAcceptedRemoveFromWaitlistHandler
{
    public static Task Handle(WaitlistOfferAccepted evt) =>
        // No-op: removal moved to BookingConfirmed handler
        Task.CompletedTask;
}
