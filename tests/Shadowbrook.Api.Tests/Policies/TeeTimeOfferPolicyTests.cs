using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Policies;

public class TeeTimeOfferPolicyTests
{
    [Fact]
    public void Start_TeeTimeRequestAdded_ReturnsNotifyCommand()
    {
        var requestId = Guid.NewGuid();
        var evt = new TeeTimeRequestAdded
        {
            TeeTimeRequestId = requestId,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 20),
            TeeTime = new TimeOnly(10, 0),
            GolfersNeeded = 2
        };

        var (policy, command) = TeeTimeOfferPolicy.Start(evt);

        Assert.Equal(requestId, policy.Id);
        Assert.False(policy.IsBuffering);
        Assert.Null(policy.CurrentOfferId);
        Assert.Equal(requestId, command.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_GolferNotifiedOfOffer_SetsStateAndReturnsTimeout()
    {
        var policy = new TeeTimeOfferPolicy { Id = Guid.NewGuid() };
        var offerId = Guid.NewGuid();
        var evt = new GolferNotifiedOfOffer
        {
            WaitlistOfferId = offerId,
            TeeTimeRequestId = policy.Id
        };

        var timeout = policy.Handle(evt);

        Assert.Equal(offerId, policy.CurrentOfferId);
        Assert.True(policy.IsBuffering);
        Assert.Equal(policy.Id, timeout.TeeTimeRequestId);
        Assert.Equal(offerId, timeout.OfferId);
    }

    [Fact]
    public void Handle_TeeTimeOfferTimeout_CurrentOffer_SendsNextCommand()
    {
        var offerId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = Guid.NewGuid(),
            CurrentOfferId = offerId,
            IsBuffering = true
        };
        var timeout = new TeeTimeOfferTimeout(policy.Id, offerId);

        var command = policy.Handle(timeout);

        Assert.NotNull(command);
        Assert.False(policy.IsBuffering);
        Assert.Equal(policy.Id, command!.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_TeeTimeOfferTimeout_StaleOffer_ReturnsNull()
    {
        var policy = new TeeTimeOfferPolicy
        {
            Id = Guid.NewGuid(),
            CurrentOfferId = Guid.NewGuid(),
            IsBuffering = true
        };
        var timeout = new TeeTimeOfferTimeout(policy.Id, Guid.NewGuid());

        var command = policy.Handle(timeout);

        Assert.Null(command);
        Assert.True(policy.IsBuffering);
    }

    [Fact]
    public void Handle_WaitlistOfferRejected_CurrentOffer_SendsNextCommand()
    {
        var offerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            CurrentOfferId = offerId,
            IsBuffering = true
        };
        var evt = new WaitlistOfferRejected
        {
            WaitlistOfferId = offerId,
            TeeTimeRequestId = requestId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        };

        var command = policy.Handle(evt);

        Assert.NotNull(command);
        Assert.False(policy.IsBuffering);
        Assert.Equal(requestId, command!.TeeTimeRequestId);
    }

    [Fact]
    public void Handle_WaitlistOfferRejected_DifferentOffer_ReturnsNull()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            CurrentOfferId = Guid.NewGuid(),
            IsBuffering = true
        };
        var evt = new WaitlistOfferRejected
        {
            WaitlistOfferId = Guid.NewGuid(),
            TeeTimeRequestId = requestId,
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        };

        var command = policy.Handle(evt);

        Assert.Null(command);
        Assert.True(policy.IsBuffering);
    }

    [Fact]
    public void Handle_TeeTimeRequestFulfilled_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            CurrentOfferId = Guid.NewGuid(),
            IsBuffering = true
        };
        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void Handle_TeeTimeRequestClosed_MarksCompleted()
    {
        var requestId = Guid.NewGuid();
        var policy = new TeeTimeOfferPolicy
        {
            Id = requestId,
            IsBuffering = false
        };
        var evt = new TeeTimeRequestClosed { TeeTimeRequestId = requestId };

        policy.Handle(evt);

        Assert.True(policy.IsCompleted());
    }
}
