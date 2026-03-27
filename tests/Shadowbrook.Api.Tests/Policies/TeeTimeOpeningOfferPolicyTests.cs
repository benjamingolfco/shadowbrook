using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class TeeTimeOpeningOfferPolicyTests
{
    [Fact]
    public void Start_InitializesPolicyAndDispatchesOffers()
    {
        var evt = new TeeTimeOpeningCreated
        {
            OpeningId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30),
            SlotsAvailable = 3
        };

        var (policy, command) = TeeTimeOpeningOfferPolicy.Start(evt);

        Assert.Equal(evt.OpeningId, policy.Id);
        Assert.Equal(3, policy.SlotsRemaining);
        Assert.Equal(0, policy.PendingOfferCount);
        Assert.Equal(evt.OpeningId, command.OpeningId);
        Assert.Equal(3, command.MaxOffers);
    }

    [Fact]
    public void Handle_OfferSent_IncrementsPendingCount()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 3 };

        policy.Handle(new WaitlistOfferSent
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = openingId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            IsWalkUp = true
        });

        Assert.Equal(1, policy.PendingOfferCount);
    }

    [Fact]
    public void Handle_OfferAccepted_DecrementsPendingCount()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 3, PendingOfferCount = 2 };

        var result = policy.Handle(new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            OpeningId = openingId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30)
        });

        Assert.Equal(1, policy.PendingOfferCount);
        Assert.Null(result); // Slot update deferred to TeeTimeOpeningSlotsClaimed — no follow-on dispatch here
    }

    [Fact]
    public void Handle_Claimed_UpdatesSlotsAndDispatchesMoreIfNeeded()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 3, PendingOfferCount = 1 };

        var result = policy.Handle(new TeeTimeOpeningSlotsClaimed
        {
            OpeningId = openingId,
            BookingId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30)
        });

        Assert.Equal(2, policy.SlotsRemaining);
        Assert.NotNull(result);
        Assert.Equal(1, result!.MaxOffers); // 2 remaining - 1 pending = 1
    }

    [Fact]
    public void Handle_OfferRejected_DecrementsPendingAndDispatchesMore()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 2, PendingOfferCount = 2 };

        var result = policy.Handle(new WaitlistOfferRejected
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = openingId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        });

        Assert.Equal(1, policy.PendingOfferCount);
        Assert.NotNull(result);
        Assert.Equal(1, result!.MaxOffers);
    }

    [Fact]
    public void Handle_OfferStale_DecrementsPendingAndDispatchesMore()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 2, PendingOfferCount = 2 };

        var result = policy.Handle(new WaitlistOfferStale
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = openingId
        });

        Assert.Equal(1, policy.PendingOfferCount);
        Assert.NotNull(result);
    }

    [Fact]
    public void Handle_Filled_MarksCompleted()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 0, PendingOfferCount = 1 };

        policy.Handle(new TeeTimeOpeningFilled { OpeningId = openingId });

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_Expired_MarksCompleted()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 2, PendingOfferCount = 1 };

        policy.Handle(new TeeTimeOpeningExpired { OpeningId = openingId });

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_WakeUp_DispatchesOffersIfSlotsAvailable()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 2, PendingOfferCount = 0 };

        var result = policy.Handle(new WakeUpOfferPolicy(openingId));

        Assert.NotNull(result);
        Assert.Equal(2, result!.MaxOffers);
    }

    [Fact]
    public void Handle_WakeUp_NoSlotsAvailable_ReturnsNull()
    {
        var openingId = Guid.NewGuid();
        var policy = new TeeTimeOpeningOfferPolicy { Id = openingId, SlotsRemaining = 1, PendingOfferCount = 1 };

        var result = policy.Handle(new WakeUpOfferPolicy(openingId));

        Assert.Null(result);
    }
}
