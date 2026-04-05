using Microsoft.Extensions.Logging.Abstractions;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Features.Waitlist.Policies;

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
    public void Handle_BufferTimeout_ReturnsMarkStaleCommandAndMarksCompleted()
    {
        var offerId = Guid.NewGuid();
        var openingId = Guid.NewGuid();
        var policy = new WaitlistOfferResponsePolicy { Id = offerId, OpeningId = openingId };

        var command = policy.Handle(new OfferResponseBufferTimeout(offerId, TimeSpan.FromSeconds(60)));

        Assert.IsType<MarkOfferStale>(command);
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
            BookingId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30)
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

    [Fact]
    public void NotFound_OfferResponseBufferTimeout_DoesNotThrow()
    {
        var timeout = new OfferResponseBufferTimeout(Guid.NewGuid(), TimeSpan.FromMinutes(5));
        var logger = NullLogger<WaitlistOfferResponsePolicy>.Instance;

        WaitlistOfferResponsePolicy.NotFound(timeout, logger);
    }

    [Fact]
    public void NotFound_WaitlistOfferAccepted_DoesNotThrow()
    {
        var evt = new WaitlistOfferAccepted
        {
            WaitlistOfferId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 1,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 3, 25),
            TeeTime = new TimeOnly(14, 30)
        };
        var logger = NullLogger<WaitlistOfferResponsePolicy>.Instance;

        WaitlistOfferResponsePolicy.NotFound(evt, logger);
    }

    [Fact]
    public void NotFound_WaitlistOfferRejected_DoesNotThrow()
    {
        var evt = new WaitlistOfferRejected
        {
            WaitlistOfferId = Guid.NewGuid(),
            OpeningId = Guid.NewGuid(),
            GolferWaitlistEntryId = Guid.NewGuid(),
            Reason = "Declined"
        };
        var logger = NullLogger<WaitlistOfferResponsePolicy>.Instance;

        WaitlistOfferResponsePolicy.NotFound(evt, logger);
    }
}
