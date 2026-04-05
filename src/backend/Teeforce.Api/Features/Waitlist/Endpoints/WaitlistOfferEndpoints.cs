using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistServices;
using Wolverine.Http;

namespace Teeforce.Api.Features.Waitlist.Endpoints;

public static class WaitlistOfferEndpoints
{
    [WolverineGet("/waitlist/offers/{token}")]
    [AllowAnonymous]
    public static async Task<IResult> GetOffer(Guid token, ApplicationDbContext db)
    {
        var offer = await db.WaitlistOffers
            .IgnoreQueryFilters()
            .Where(o => o.Token == token)
            .Join(db.TeeTimeOpenings, o => o.OpeningId, op => op.Id, (o, op) => new { Offer = o, Opening = op })
            .Join(db.GolferWaitlistEntries, oo => oo.Offer.GolferWaitlistEntryId, e => e.Id, (oo, e) => new { oo.Offer, oo.Opening, Entry = e })
            .Join(db.Golfers.IgnoreQueryFilters(), ooe => ooe.Entry.GolferId, g => g.Id, (ooe, g) => new { ooe.Offer, ooe.Opening, ooe.Entry, Golfer = g })
            .Join(db.Courses.IgnoreQueryFilters(), ooeg => ooeg.Opening.CourseId, c => c.Id, (ooeg, c) => new
            {
                ooeg.Offer.Token,
                CourseName = c.Name,
                TeeTime = ooeg.Opening.TeeTime.Value,
                ooeg.Opening.SlotsAvailable,
                ooeg.Golfer.FirstName,
                ooeg.Golfer.LastName,
                ooeg.Offer.Status
            })
            .FirstOrDefaultAsync();

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        return Results.Ok(new WaitlistOfferResponse(
            offer.Token,
            offer.CourseName,
            offer.TeeTime,
            offer.SlotsAvailable,
            $"{offer.FirstName} {offer.LastName}",
            offer.Status.ToString()));
    }

    [WolverinePost("/waitlist/offers/{token}/accept")]
    [AllowAnonymous]
    public static async Task<IResult> AcceptOffer(
        Guid token,
        IWaitlistOfferRepository offerRepository,
        ITeeTimeOpeningRepository openingRepository,
        [NotBody] WaitlistOfferClaimService claimService)
    {
        var offer = await offerRepository.GetByTokenAsync(token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        var opening = await openingRepository.GetRequiredByIdAsync(offer.OpeningId);
        var result = claimService.AcceptOffer(offer, opening);

        if (!result.Success)
        {
            return Results.Conflict(new { reason = result.Reason });
        }

        return Results.Ok(new WaitlistOfferAcceptResponse(
            "Confirmed",
            "Your tee time has been confirmed. See you on the course!",
            offer.GolferId));
    }
}

public record WaitlistOfferResponse(
    Guid Token,
    string CourseName,
    DateTime TeeTime,
    int SlotsAvailable,
    string GolferName,
    string Status);

public record WaitlistOfferAcceptResponse(
    string Status,
    string Message,
    Guid GolferId);

