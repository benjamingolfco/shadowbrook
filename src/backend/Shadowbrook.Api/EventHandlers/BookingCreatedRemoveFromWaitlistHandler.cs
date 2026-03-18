using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public static class BookingCreatedRemoveFromWaitlistHandler
{
    public static async Task Handle(
        BookingCreated domainEvent,
        IWaitlistOfferRepository offerRepository,
        IGolferWaitlistEntryRepository entryRepository)
    {
        var offer = await offerRepository.GetByBookingIdAsync(domainEvent.BookingId);
        if (offer is null)
        {
            return;
        }

        var entry = await entryRepository.GetByIdAsync(offer.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        entry.Remove();
    }
}
