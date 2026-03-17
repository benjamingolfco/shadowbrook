using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;

namespace Shadowbrook.Api.EventHandlers;

public class WaitlistOfferAcceptedFillHandler(
    ITeeTimeRequestRepository requestRepository,
    IGolferWaitlistEntryRepository entryRepository,
    IMessageBus bus)
{
    public async Task Handle(WaitlistOfferAccepted domainEvent, CancellationToken ct)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            await bus.PublishAsync(new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = "Tee time request not found."
            });
            return;
        }

        var result = request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId);

        if (!result.Success)
        {
            await bus.PublishAsync(new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = result.RejectionReason ?? "Unable to fill slot."
            });
            return;
        }

        await requestRepository.SaveAsync();

        await bus.PublishAsync(new TeeTimeSlotFilled
        {
            TeeTimeRequestId = domainEvent.TeeTimeRequestId,
            BookingId = domainEvent.BookingId,
            GolferId = domainEvent.GolferId
        });
    }
}
