using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Waitlist.Endpoints;

public static class WaitlistOfferEndpoints
{
    [WolverineGet("/waitlist/offers/{token}")]
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
                ooeg.Opening.Date,
                ooeg.Opening.TeeTime,
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
            offer.Date.ToString("yyyy-MM-dd"),
            offer.TeeTime.ToString("HH:mm"),
            offer.SlotsAvailable,
            $"{offer.FirstName} {offer.LastName}",
            offer.Status.ToString()));
    }

    [WolverinePost("/waitlist/offers/{token}/accept")]
    public static async Task<IResult> AcceptOffer(
        Guid token,
        IWaitlistOfferRepository offerRepository)
    {
        var offer = await offerRepository.GetByTokenAsync(token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        offer.Accept();

        return Results.Ok(new WaitlistOfferAcceptResponse(
            "Processing",
            "We're processing your request — you'll receive a confirmation shortly."));
    }
}

public record WaitlistOfferResponse(
    Guid Token,
    string CourseName,
    string Date,
    string TeeTime,
    int SlotsAvailable,
    string GolferName,
    string Status);

public record WaitlistOfferAcceptResponse(
    string Status,
    string Message);
