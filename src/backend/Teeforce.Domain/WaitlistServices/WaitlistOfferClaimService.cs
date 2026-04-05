using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Domain.WaitlistServices;

public class WaitlistOfferClaimService(ITimeProvider timeProvider)
{
    public ClaimResult AcceptOffer(WaitlistOffer offer, TeeTimeOpening opening)
    {
        var result = opening.TryClaim(offer.BookingId, offer.GolferId, offer.GroupSize, timeProvider);

        if (result.Success)
        {
            offer.Accept();
        }
        else
        {
            offer.Reject(result.Reason!);
        }

        return result;
    }
}
