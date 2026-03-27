using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Shadowbrook.Api.Features.Waitlist.Policies;

public class TeeTimeOpeningOfferPolicy : Saga
{
    public Guid Id { get; set; }
    public int PendingOfferCount { get; set; }
    public int SlotsRemaining { get; set; }

    public static (TeeTimeOpeningOfferPolicy, FindAndOfferEligibleGolfers) Start(TeeTimeOpeningCreated evt)
    {
        var policy = new TeeTimeOpeningOfferPolicy
        {
            Id = evt.OpeningId,
            SlotsRemaining = evt.SlotsAvailable
        };

        return (policy, new FindAndOfferEligibleGolfers(evt.OpeningId, evt.SlotsAvailable));
    }

    public void Handle([SagaIdentityFrom("OpeningId")] WaitlistOfferSent evt) => PendingOfferCount++;

    public FindAndOfferEligibleGolfers? Handle([SagaIdentityFrom("OpeningId")] WaitlistOfferAccepted evt)
    {
        PendingOfferCount = Math.Max(0, PendingOfferCount - 1);
        return null; // Slots updated via TeeTimeOpeningSlotsClaimed
    }

    public FindAndOfferEligibleGolfers? Handle([SagaIdentityFrom("OpeningId")] TeeTimeOpeningSlotsClaimed evt)
    {
        SlotsRemaining = Math.Max(0, SlotsRemaining - 1);
        return DispatchMoreOffersIfNeeded();
    }

    public FindAndOfferEligibleGolfers? Handle([SagaIdentityFrom("OpeningId")] WaitlistOfferRejected evt)
    {
        PendingOfferCount = Math.Max(0, PendingOfferCount - 1);
        return DispatchMoreOffersIfNeeded();
    }

    public FindAndOfferEligibleGolfers? Handle([SagaIdentityFrom("OpeningId")] WaitlistOfferStale evt)
    {
        PendingOfferCount = Math.Max(0, PendingOfferCount - 1);
        return DispatchMoreOffersIfNeeded();
    }

    public void Handle([SagaIdentityFrom("OpeningId")] TeeTimeOpeningFilled evt) => MarkCompleted();

    public void Handle([SagaIdentityFrom("OpeningId")] TeeTimeOpeningExpired evt) => MarkCompleted();

    public FindAndOfferEligibleGolfers? Handle([SagaIdentityFrom("OpeningId")] WakeUpOfferPolicy command) => DispatchMoreOffersIfNeeded();

    private FindAndOfferEligibleGolfers? DispatchMoreOffersIfNeeded()
    {
        var availableSlots = SlotsRemaining - PendingOfferCount;
        if (availableSlots <= 0)
        {
            return null;
        }

        return new FindAndOfferEligibleGolfers(Id, availableSlots);
    }
}

public record WakeUpOfferPolicy(Guid OpeningId);
