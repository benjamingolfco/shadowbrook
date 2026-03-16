using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public class WaitlistOfferAcceptedFillHandler(
    ITeeTimeRequestRepository requestRepository,
    IGolferWaitlistEntryRepository entryRepository,
    IDomainEventPublisher eventPublisher)
    : IDomainEventHandler<WaitlistOfferAccepted>
{
    public async Task HandleAsync(WaitlistOfferAccepted domainEvent, CancellationToken ct = default)
    {
        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            return;
        }

        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var result = request.Fill(domainEvent.GolferId, entry.GroupSize, domainEvent.BookingId);
        await requestRepository.SaveAsync();

        if (result.Success)
        {
            await eventPublisher.PublishAsync(new TeeTimeSlotFilled
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                BookingId = domainEvent.BookingId,
                GolferId = domainEvent.GolferId
            }, ct);
        }
        else
        {
            await eventPublisher.PublishAsync(new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = result.RejectionReason!
            }, ct);
        }
    }
}
