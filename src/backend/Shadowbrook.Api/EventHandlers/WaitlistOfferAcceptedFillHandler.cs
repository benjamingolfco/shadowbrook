using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
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
    IDomainEventPublisher eventPublisher,
    ApplicationDbContext db)
    : IDomainEventHandler<WaitlistOfferAccepted>
{
    public async Task HandleAsync(WaitlistOfferAccepted domainEvent, CancellationToken ct = default)
    {
        var entry = await entryRepository.GetByIdAsync(domainEvent.GolferWaitlistEntryId);
        if (entry is null)
        {
            return;
        }

        var result = await TryFillAsync(domainEvent, entry.GroupSize);

        if (result is null)
        {
            // Concurrency conflict on both attempts — reload and retry once more
            db.ChangeTracker.Clear();
            result = await TryFillAsync(domainEvent, entry.GroupSize);
        }

        if (result is null || !result.Success)
        {
            await eventPublisher.PublishAsync(new TeeTimeSlotFillFailed
            {
                TeeTimeRequestId = domainEvent.TeeTimeRequestId,
                OfferId = domainEvent.WaitlistOfferId,
                Reason = result?.RejectionReason ?? "Concurrent update conflict."
            }, ct);
            return;
        }

        await eventPublisher.PublishAsync(new TeeTimeSlotFilled
        {
            TeeTimeRequestId = domainEvent.TeeTimeRequestId,
            BookingId = domainEvent.BookingId,
            GolferId = domainEvent.GolferId
        }, ct);
    }

    private async Task<FillResult?> TryFillAsync(WaitlistOfferAccepted domainEvent, int groupSize)
    {
        var request = await requestRepository.GetByIdAsync(domainEvent.TeeTimeRequestId);
        if (request is null)
        {
            return new FillResult(false, "Tee time request not found.");
        }

        var result = request.Fill(domainEvent.GolferId, groupSize, domainEvent.BookingId);

        try
        {
            await requestRepository.SaveAsync();
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Signal to the caller that we lost the race — caller will clear the tracker and retry
            return null;
        }
    }
}
