using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.EventHandlers;

public class BookingCreatedRemoveFromWaitlistHandler(
    IWaitlistOfferRepository offerRepository,
    IGolferWaitlistEntryRepository entryRepository)
    : IDomainEventHandler<BookingCreated>
{
    public async Task HandleAsync(BookingCreated domainEvent, CancellationToken ct = default)
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
        await entryRepository.SaveAsync();
    }
}
