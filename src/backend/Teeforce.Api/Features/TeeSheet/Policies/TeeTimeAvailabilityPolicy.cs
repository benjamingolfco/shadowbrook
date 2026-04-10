using Microsoft.Extensions.Logging;
using Teeforce.Domain.TeeTimeAggregate.Events;
using Teeforce.Domain.TeeTimeOfferAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Teeforce.Api.Features.TeeSheet.Policies;

public class TeeTimeAvailabilityPolicy : Saga
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

    public Guid Id { get; set; }
    public HashSet<Guid> PendingOfferIds { get; set; } = [];
    public int SlotsRemaining { get; set; }
    public bool GracePeriodExpired { get; set; }
    public Guid CourseId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    public static (TeeTimeAvailabilityPolicy, AvailabilityGracePeriodTimeout) Start(TeeTimeClaimReleased evt)
    {
        var policy = new TeeTimeAvailabilityPolicy
        {
            Id = evt.TeeTimeId,
            CourseId = evt.CourseId,
            Date = evt.Date,
            Time = evt.Time,
        };

        return (policy, new AvailabilityGracePeriodTimeout(policy.Id, GracePeriod));
    }

    public FindAndOfferForTeeTime Handle(AvailabilityGracePeriodTimeout timeout)
    {
        GracePeriodExpired = true;
        return new FindAndOfferForTeeTime(Id, SlotsRemaining - PendingOfferIds.Count, CourseId, Date, Time);
    }

    public FindAndOfferForTeeTime? Handle([SagaIdentityFrom("TeeTimeId")] TeeTimeAvailabilityChanged evt)
    {
        SlotsRemaining = evt.Remaining;

        if (SlotsRemaining == 0)
        {
            MarkCompleted();
            return null;
        }

        return DispatchMoreOffersIfNeeded();
    }

    public void Handle([SagaIdentityFrom("TeeTimeId")] TeeTimeOfferSent evt) =>
        PendingOfferIds.Add(evt.TeeTimeOfferId);

    public FindAndOfferForTeeTime? Handle([SagaIdentityFrom("TeeTimeId")] TeeTimeOfferAccepted evt)
    {
        PendingOfferIds.Remove(evt.TeeTimeOfferId);
        return DispatchMoreOffersIfNeeded();
    }

    public FindAndOfferForTeeTime? Handle([SagaIdentityFrom("TeeTimeId")] TeeTimeOfferRejected evt)
    {
        PendingOfferIds.Remove(evt.TeeTimeOfferId);
        return DispatchMoreOffersIfNeeded();
    }

    public FindAndOfferForTeeTime? Handle([SagaIdentityFrom("TeeTimeId")] TeeTimeOfferExpired evt)
    {
        PendingOfferIds.Remove(evt.TeeTimeOfferId);
        return DispatchMoreOffersIfNeeded();
    }

    public FindAndOfferForTeeTime? Handle([SagaIdentityFrom("TeeTimeId")] TeeTimeOfferStale evt)
    {
        PendingOfferIds.Remove(evt.TeeTimeOfferId);
        return DispatchMoreOffersIfNeeded();
    }

    public static void NotFound(
        AvailabilityGracePeriodTimeout timeout,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {MessageType} for {Id}",
            nameof(AvailabilityGracePeriodTimeout), timeout.Id);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeClaimReleased evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeClaimReleased), evt.TeeTimeId);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeAvailabilityChanged evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeAvailabilityChanged), evt.TeeTimeId);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeOfferSent evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeOfferSent), evt.TeeTimeId);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeOfferAccepted evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeOfferAccepted), evt.TeeTimeId);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeOfferRejected evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeOfferRejected), evt.TeeTimeId);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeOfferExpired evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeOfferExpired), evt.TeeTimeId);

    public static void NotFound(
        [SagaIdentityFrom("TeeTimeId")] TeeTimeOfferStale evt,
        ILogger<TeeTimeAvailabilityPolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for TeeTime {TeeTimeId}",
            nameof(TeeTimeOfferStale), evt.TeeTimeId);

    private FindAndOfferForTeeTime? DispatchMoreOffersIfNeeded()
    {
        if (!GracePeriodExpired)
        {
            return null;
        }

        var availableSlots = SlotsRemaining - PendingOfferIds.Count;
        if (availableSlots <= 0)
        {
            return null;
        }

        return new FindAndOfferForTeeTime(Id, availableSlots, CourseId, Date, Time);
    }
}

public record FindAndOfferForTeeTime(
    Guid TeeTimeId,
    int AvailableSlots,
    Guid CourseId,
    DateOnly Date,
    TimeOnly Time);

public record AvailabilityGracePeriodTimeout(Guid Id, TimeSpan Delay) : TimeoutMessage(Delay);
