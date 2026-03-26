using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class WaitlistOfferResponsePolicyTests
{
    [Fact]
    public void Start_WalkUp_SchedulesSixtySecondBuffer()
    {
        var evt = new WaitlistOfferSent
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            IsWalkUp = true
        };

        var (policy, timeout) = WaitlistOfferResponsePolicy.Start(evt);

        Assert.Equal(evt.WaitlistOfferId, policy.Id);
        Assert.Equal(evt.OpeningId, policy.OpeningId);
        Assert.Equal(TimeSpan.FromSeconds(60), timeout.Buffer);
    }

    [Fact]
    public void Start_Online_SchedulesTenMinuteBuffer()
    {
        var evt = new WaitlistOfferSent
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            IsWalkUp = false
        };

        var (_, timeout) = WaitlistOfferResponsePolicy.Start(evt);

        Assert.Equal(TimeSpan.FromMinutes(10), timeout.Buffer);
    }

    [Fact]
    public void Handle_BufferTimeout_ReturnsRejectCommandAndMarksCompleted()
    {
        var offerId = Guid.NewGuid();
        var openingId = Guid.NewGuid();
        var policy = new WaitlistOfferResponsePolicy { Id = offerId, OpeningId = openingId };

        var command = policy.Handle(new OfferResponseBufferTimeout(offerId, openingId, TimeSpan.FromSeconds(60)));

        Assert.IsType<RejectStaleOffer>(command);
        Assert.Equal(offerId, command.WaitlistOfferId);
        Assert.Equal(openingId, command.OpeningId);
        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_OfferAccepted_MarksCompleted()
    {
        var offerId = Guid.NewGuid();
        var policy = new WaitlistOfferResponsePolicy { Id = offerId, OpeningId = Guid.NewGuid() };

        policy.Handle(new WaitlistOfferAccepted
        {
            WaitlistOfferId = offerId,
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1
        });

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_OfferRejected_MarksCompleted()
    {
        var offerId = Guid.NewGuid();
        var policy = new WaitlistOfferResponsePolicy { Id = offerId, OpeningId = Guid.NewGuid() };

        policy.Handle(new WaitlistOfferRejected
        {
            WaitlistOfferId = offerId,
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        });

        Assert.True(policy.IsCompleted());
    }
}
