using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public class TeeTimeOfferPolicy : Saga
{
    public Guid Id { get; set; }
    public Guid? LastOfferId { get; set; }

    public static (TeeTimeOfferPolicy, NotifyNextEligibleGolfer) Start(TeeTimeRequestAdded evt)
    {
        var policy = new TeeTimeOfferPolicy { Id = evt.TeeTimeRequestId };
        return (policy, new NotifyNextEligibleGolfer(evt.TeeTimeRequestId));
    }

    public TeeTimeOfferBufferTimeout Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] GolferNotifiedOfOffer evt)
    {
        LastOfferId = evt.WaitlistOfferId;
        return new TeeTimeOfferBufferTimeout(Id, evt.WaitlistOfferId);
    }

    public NotifyNextEligibleGolfer? Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeOfferBufferTimeout timeout)
    {
        if (timeout.OfferId != LastOfferId)
        {
            return null;
        }

        return new NotifyNextEligibleGolfer(Id);
    }

    public NotifyNextEligibleGolfer? Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] WaitlistOfferRejected evt)
    {
        if (evt.WaitlistOfferId != LastOfferId)
        {
            return null;
        }

        return new NotifyNextEligibleGolfer(Id);
    }

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] WaitlistOfferAccepted evt)
    {
        if (evt.WaitlistOfferId != LastOfferId)
        {
            return;
        }

        LastOfferId = null;
    }

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestFulfilled evt) => MarkCompleted();

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestClosed evt) => MarkCompleted();
}

public record NotifyNextEligibleGolfer(Guid TeeTimeRequestId);

public record TeeTimeOfferBufferTimeout(Guid TeeTimeRequestId, Guid OfferId)
    : TimeoutMessage(TimeSpan.FromSeconds(60));
