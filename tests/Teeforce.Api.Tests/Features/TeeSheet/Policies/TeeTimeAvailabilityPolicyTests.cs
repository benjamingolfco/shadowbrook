using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.TeeSheet.Policies;
using Teeforce.Domain.TeeTimeAggregate.Events;
using Teeforce.Domain.TeeTimeOfferAggregate.Events;

namespace Teeforce.Api.Tests.Features.TeeSheet.Policies;

public class TeeTimeAvailabilityPolicyTests
{
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 6, 1);
    private static readonly TimeOnly Time = new(9, 0);

    [Fact]
    public void Start_SetsPolicyIdToTeeTimeId()
    {
        var teeTimeId = Guid.NewGuid();
        var evt = MakeClaimReleased(teeTimeId);

        var (policy, timeout) = TeeTimeAvailabilityPolicy.Start(evt);

        Assert.Equal(teeTimeId, policy.Id);
        Assert.False(policy.GracePeriodExpired);
        Assert.Empty(policy.PendingOfferIds);
        Assert.Equal(CourseId, policy.CourseId);
        Assert.Equal(Date, policy.Date);
        Assert.Equal(Time, policy.Time);
    }

    [Fact]
    public void Start_ReturnsGracePeriodTimeout()
    {
        var teeTimeId = Guid.NewGuid();
        var evt = MakeClaimReleased(teeTimeId);

        var (policy, timeout) = TeeTimeAvailabilityPolicy.Start(evt);

        Assert.Equal(policy.Id, timeout.Id);
        Assert.True(timeout.Delay > TimeSpan.Zero);
    }

    [Fact]
    public void GracePeriodTimeout_SetsExpiredAndDispatches()
    {
        var teeTimeId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 3);

        var command = policy.Handle(new AvailabilityGracePeriodTimeout(teeTimeId, TimeSpan.FromSeconds(5)));

        Assert.True(policy.GracePeriodExpired);
        Assert.NotNull(command);
        Assert.Equal(teeTimeId, command.TeeTimeId);
        Assert.Equal(3, command.AvailableSlots);
        Assert.Equal(CourseId, command.CourseId);
        Assert.Equal(Date, command.Date);
        Assert.Equal(Time, command.Time);
    }

    [Fact]
    public void AvailabilityChanged_UpdatesSlotsRemaining()
    {
        var teeTimeId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 2, gracePeriodExpired: true);

        policy.Handle(new TeeTimeAvailabilityChanged
        {
            TeeTimeId = teeTimeId,
            Remaining = 4,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            Time = new TimeOnly(9, 0),
        });

        Assert.Equal(4, policy.SlotsRemaining);
    }

    [Fact]
    public void AvailabilityChanged_ZeroRemaining_MarksPolicyCompleted()
    {
        var teeTimeId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 2);

        policy.Handle(new TeeTimeAvailabilityChanged
        {
            TeeTimeId = teeTimeId,
            Remaining = 0,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            Time = new TimeOnly(9, 0),
        });

        Assert.True(policy.IsCompleted());
    }

    [Fact]
    public void OfferSent_AddsToPendingOfferIds()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 2, gracePeriodExpired: true);

        policy.Handle(new TeeTimeOfferSent
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
            GolferId = Guid.NewGuid(),
            GroupSize = 2,
        });

        Assert.Contains(offerId, policy.PendingOfferIds);
    }

    [Fact]
    public void OfferAccepted_RemovesFromPending()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 2, gracePeriodExpired: true);
        policy.PendingOfferIds.Add(offerId);

        policy.Handle(new TeeTimeOfferAccepted
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
            BookingId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 2,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            Time = new TimeOnly(9, 0),
        });

        Assert.DoesNotContain(offerId, policy.PendingOfferIds);
    }

    [Fact]
    public void OfferRejected_RemovesFromPendingAndDispatchesIfSlotsAvailable()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 2, gracePeriodExpired: true);
        policy.PendingOfferIds.Add(offerId);

        var command = policy.Handle(new TeeTimeOfferRejected
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
            Reason = "declined",
        });

        Assert.DoesNotContain(offerId, policy.PendingOfferIds);
        Assert.NotNull(command);
        Assert.Equal(2, command.AvailableSlots);
    }

    [Fact]
    public void OfferExpired_RemovesFromPendingAndDispatchesIfSlotsAvailable()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 1, gracePeriodExpired: true);
        policy.PendingOfferIds.Add(offerId);

        var command = policy.Handle(new TeeTimeOfferExpired
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
        });

        Assert.DoesNotContain(offerId, policy.PendingOfferIds);
        Assert.NotNull(command);
        Assert.Equal(1, command.AvailableSlots);
    }

    [Fact]
    public void OfferStale_RemovesFromPendingAndDispatchesIfSlotsAvailable()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 1, gracePeriodExpired: true);
        policy.PendingOfferIds.Add(offerId);

        var command = policy.Handle(new TeeTimeOfferStale
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
        });

        Assert.DoesNotContain(offerId, policy.PendingOfferIds);
        Assert.NotNull(command);
    }

    [Fact]
    public void DispatchSuppressed_WhenGracePeriodNotExpired()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 2, gracePeriodExpired: false);
        policy.PendingOfferIds.Add(offerId);

        var command = policy.Handle(new TeeTimeOfferRejected
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
            Reason = "declined",
        });

        Assert.Null(command);
    }

    [Fact]
    public void DispatchSuppressed_WhenNoSlotsAvailable()
    {
        var teeTimeId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var policy = MakePolicy(teeTimeId, slotsRemaining: 1, gracePeriodExpired: true);
        policy.PendingOfferIds.Add(offerId);
        policy.PendingOfferIds.Add(Guid.NewGuid()); // second pending = slotsRemaining(1) - pending(1 after remove) = 0

        var command = policy.Handle(new TeeTimeOfferRejected
        {
            TeeTimeOfferId = offerId,
            TeeTimeId = teeTimeId,
            Reason = "declined",
        });

        Assert.Null(command);
    }

    [Fact]
    public void NotFound_GracePeriodTimeout_LogsAndDoesNotThrow()
    {
        var logger = Substitute.For<ILogger<TeeTimeAvailabilityPolicy>>();
        TeeTimeAvailabilityPolicy.NotFound(
            new AvailabilityGracePeriodTimeout(Guid.NewGuid(), TimeSpan.FromSeconds(5)),
            logger);
    }

    [Fact]
    public void NotFound_TeeTimeClaimReleased_LogsAndDoesNotThrow()
    {
        var logger = Substitute.For<ILogger<TeeTimeAvailabilityPolicy>>();
        TeeTimeAvailabilityPolicy.NotFound(
            new TeeTimeClaimReleased
            {
                TeeTimeId = Guid.NewGuid(),
                BookingId = Guid.NewGuid(),
                GolferId = Guid.NewGuid(),
                GroupSize = 2,
                CourseId = Guid.NewGuid(),
                Date = new DateOnly(2026, 6, 1),
                Time = new TimeOnly(9, 0),
            },
            logger);
    }

    private static TeeTimeClaimReleased MakeClaimReleased(Guid teeTimeId) => new()
    {
        TeeTimeId = teeTimeId,
        BookingId = Guid.NewGuid(),
        GolferId = Guid.NewGuid(),
        GroupSize = 2,
        CourseId = CourseId,
        Date = Date,
        Time = Time,
    };

    private static TeeTimeAvailabilityPolicy MakePolicy(
        Guid teeTimeId,
        int slotsRemaining,
        bool gracePeriodExpired = false)
    {
        return new TeeTimeAvailabilityPolicy
        {
            Id = teeTimeId,
            SlotsRemaining = slotsRemaining,
            GracePeriodExpired = gracePeriodExpired,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        };
    }
}
