using Wolverine;

namespace Shadowbrook.Api.Features.WaitlistOffers;

public class TeeTimeOfferPolicy : Saga
{
    public Guid Id { get; set; }
    public Guid? CurrentOfferId { get; set; }
    public bool IsBuffering { get; set; }
}
