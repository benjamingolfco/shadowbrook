using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.Waitlist.Policies;

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
        var timeout = new OfferResponseBufferTimeout(buffer);

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
}

public record OfferResponseBufferTimeout(TimeSpan Buffer) : TimeoutMessage(Buffer);

public record MarkOfferStale(Guid WaitlistOfferId, Guid OpeningId);
