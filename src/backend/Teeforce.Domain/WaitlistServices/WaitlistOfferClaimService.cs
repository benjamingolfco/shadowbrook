using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;

namespace Teeforce.Domain.WaitlistServices;

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
