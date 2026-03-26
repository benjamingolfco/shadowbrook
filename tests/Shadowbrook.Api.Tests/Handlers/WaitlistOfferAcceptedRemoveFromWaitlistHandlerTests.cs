using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

// DEPRECATED: These tests are now no-ops because waitlist removal moved to BookingConfirmed
// Keeping for backward compatibility, but the handler now does nothing
public class WaitlistOfferAcceptedRemoveFromWaitlistHandlerTests
{
    [Fact]
    public async Task Handle_NoOp_Completes()
    {
        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1
        };

        await WaitlistOfferAcceptedRemoveFromWaitlistHandler.Handle(evt);

        // Handler is now a no-op, just verify it completes without error
        Assert.True(true);
    }
}
