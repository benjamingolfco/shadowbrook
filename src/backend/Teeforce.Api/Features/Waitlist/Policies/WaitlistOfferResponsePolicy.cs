using Microsoft.Extensions.Logging;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Domain.WaitlistOfferAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Teeforce.Api.Features.Waitlist.Policies;

public class WaitlistOfferResponsePolicy : Saga
{
    private static readonly TimeSpan WalkUpBuffer = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OnlineBuffer = TimeSpan.FromMinutes(10);

    public Guid Id { get; set; }
    public Guid OpeningId { get; set; }

    public static (WaitlistOfferResponsePolicy, OfferResponseBufferTimeout) Start(WaitlistOfferSent evt)
    {
        var policy = new WaitlistOfferResponsePolicy
        {
            Id = evt.WaitlistOfferId,
            OpeningId = evt.OpeningId
        };

        var buffer = evt.IsWalkUp ? WalkUpBuffer : OnlineBuffer;
        var timeout = new OfferResponseBufferTimeout(policy.Id, buffer);

        return (policy, timeout);
    }

    public MarkOfferStale Handle(OfferResponseBufferTimeout timeout)
    {
        MarkCompleted();
        return new MarkOfferStale(Id, OpeningId);
    }

    public void Handle(
        [SagaIdentityFrom("WaitlistOfferId")] WaitlistOfferAccepted evt) => MarkCompleted();

    public void Handle(
        [SagaIdentityFrom("WaitlistOfferId")] WaitlistOfferRejected evt) => MarkCompleted();

    public static void NotFound(OfferResponseBufferTimeout timeout, ILogger<WaitlistOfferResponsePolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {MessageType} for {Id}", nameof(OfferResponseBufferTimeout), timeout.Id);

    public static void NotFound(
        [SagaIdentityFrom("WaitlistOfferId")] WaitlistOfferAccepted evt,
        ILogger<WaitlistOfferResponsePolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for offer {WaitlistOfferId}", nameof(WaitlistOfferAccepted), evt.WaitlistOfferId);

    public static void NotFound(
        [SagaIdentityFrom("WaitlistOfferId")] WaitlistOfferRejected evt,
        ILogger<WaitlistOfferResponsePolicy> logger) =>
        logger.LogInformation("Policy already completed, ignoring {EventType} for offer {WaitlistOfferId}", nameof(WaitlistOfferRejected), evt.WaitlistOfferId);
}

public record OfferResponseBufferTimeout(Guid Id, TimeSpan Buffer) : TimeoutMessage(Buffer);
