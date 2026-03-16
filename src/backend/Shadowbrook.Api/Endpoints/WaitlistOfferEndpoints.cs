using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Endpoints;

public static class WaitlistOfferEndpoints
{
    public static void MapWaitlistOfferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/waitlist/offers");

        group.MapGet("/{token:guid}", GetOffer);
        group.MapPost("/{token:guid}/accept", AcceptOffer);
    }

    private static async Task<IResult> GetOffer(Guid token, IWaitlistOfferRepository repository)
    {
        var offer = await repository.GetByTokenAsync(token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        return Results.Ok(new WaitlistOfferResponse(
            offer.Token,
            offer.BookingId,
            offer.Status.ToString()));
    }

    private static async Task<IResult> AcceptOffer(
        Guid token,
        IWaitlistOfferRepository repository,
        CancellationToken ct)
    {
        var offer = await repository.GetByTokenAsync(token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        // TODO: Rewrite in later task — full accept flow with Golfer lookup and booking creation
        return Results.Ok(new { status = "Accepted" });
    }
}

public record WaitlistOfferResponse(
    Guid Token,
    Guid BookingId,
    string Status);
