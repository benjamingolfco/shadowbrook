using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public class TeeTimeOfferPolicy : Saga
{
    public Guid Id { get; set; }
    public Guid? CurrentOfferId { get; set; }
    public bool IsBuffering { get; set; }

    public static (TeeTimeOfferPolicy, NotifyNextEligibleGolfer) Start(TeeTimeRequestAdded evt)
    {
        var policy = new TeeTimeOfferPolicy { Id = evt.TeeTimeRequestId };
        return (policy, new NotifyNextEligibleGolfer(evt.TeeTimeRequestId));
    }

    public TeeTimeOfferTimeout Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] GolferNotifiedOfOffer evt)
    {
        CurrentOfferId = evt.WaitlistOfferId;
        IsBuffering = true;
        return new TeeTimeOfferTimeout(Id, evt.WaitlistOfferId);
    }

    public NotifyNextEligibleGolfer? Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeOfferTimeout timeout)
    {
        if (timeout.OfferId != CurrentOfferId) return null;
        IsBuffering = false;
        return new NotifyNextEligibleGolfer(Id);
    }

    public NotifyNextEligibleGolfer? Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] WaitlistOfferRejected evt)
    {
        if (evt.WaitlistOfferId != CurrentOfferId) return null;
        IsBuffering = false;
        return new NotifyNextEligibleGolfer(Id);
    }

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestFulfilled evt) => MarkCompleted();

    public void Handle(
        [SagaIdentityFrom("TeeTimeRequestId")] TeeTimeRequestClosed evt) => MarkCompleted();
}

public record NotifyNextEligibleGolfer(Guid TeeTimeRequestId);

public record TeeTimeOfferTimeout(Guid TeeTimeRequestId, Guid OfferId)
    : TimeoutMessage(TimeSpan.FromSeconds(60));
