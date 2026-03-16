using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.EventHandlers;

public class WaitlistOfferAcceptedHandler(
    ApplicationDbContext db,
    IWaitlistOfferRepository repository,
    ITextMessageService textMessageService)
    : IDomainEventHandler<WaitlistOfferAccepted>
{
    public async Task HandleAsync(WaitlistOfferAccepted domainEvent, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Create booking
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            CourseId = domainEvent.CourseId,
            Date = domainEvent.Date,
            Time = domainEvent.TeeTime,
            GolferName = domainEvent.GolferName,
            PlayerCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Bookings.Add(booking);

        // Check if all slots are filled
        if (domainEvent.AcceptanceCount >= domainEvent.GolfersNeeded)
        {
            // Mark request as fulfilled
            var request = await db.TeeTimeRequests.FindAsync([domainEvent.TeeTimeRequestId], ct);
            if (request is not null)
            {
                request.MarkFulfilled();
            }

            // Expire all other pending offers for this request
            var pendingOffers = await repository.GetPendingByRequestAsync(domainEvent.TeeTimeRequestId);

            foreach (var offer in pendingOffers)
            {
                offer.Expire();
            }
        }

        // Soft-delete the golfer's waitlist entry
        var golferEntry = await db.GolferWaitlistEntries.FindAsync([domainEvent.GolferWaitlistEntryId], ct);
        if (golferEntry is not null)
        {
            golferEntry.Remove();
        }

        await db.SaveChangesAsync(ct);

        // Send confirmation SMS
        var message = $"You're booked! {domainEvent.CourseName} at {domainEvent.TeeTime:h:mm tt} on {domainEvent.Date:MMMM d}. See you on the course!";
        await textMessageService.SendAsync(domainEvent.GolferPhone, message, ct);
    }
}
