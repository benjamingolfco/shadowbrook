using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class WaitlistOfferEndpoints
{
    [WolverineGet("/waitlist/offers/{token}")]
    public static async Task<IResult> GetOffer(Guid token, [NotBody] ApplicationDbContext db)
    {
        var raw = await db.WaitlistOffers
            .IgnoreQueryFilters()
            .Where(o => o.Token == token)
            .Join(db.TeeTimeRequests, o => o.TeeTimeRequestId, r => r.Id, (o, r) => new { Offer = o, Request = r })
            .Join(db.GolferWaitlistEntries, or => or.Offer.GolferWaitlistEntryId, e => e.Id, (or, e) => new { or.Offer, or.Request, Entry = e })
            .Join(db.Golfers.IgnoreQueryFilters(), ore => ore.Entry.GolferId, g => g.Id, (ore, g) => new { ore.Offer, ore.Request, ore.Entry, Golfer = g })
            .Join(db.Courses.IgnoreQueryFilters(), oreg => oreg.Request.CourseId, c => c.Id, (oreg, c) => new
            {
                oreg.Offer.Token,
                CourseName = c.Name,
                oreg.Request.Date,
                oreg.Request.TeeTime,
                oreg.Request.GolfersNeeded,
                oreg.Golfer.FirstName,
                oreg.Golfer.LastName,
                oreg.Offer.Status
            })
            .FirstOrDefaultAsync();

        if (raw is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        return Results.Ok(new WaitlistOfferResponse(
            raw.Token,
            raw.CourseName,
            raw.Date.ToString("yyyy-MM-dd"),
            raw.TeeTime.ToString("HH:mm"),
            raw.GolfersNeeded,
            $"{raw.FirstName} {raw.LastName}",
            raw.Status.ToString()));
    }

    [WolverinePost("/waitlist/offers/{token}/accept")]
    public static async Task<IResult> AcceptOffer(
        Guid token,
        IWaitlistOfferRepository offerRepository,
        IGolferWaitlistEntryRepository entryRepository,
        IGolferRepository golferRepository)
    {
        var offer = await offerRepository.GetByTokenAsync(token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        var entry = await entryRepository.GetByIdAsync(offer.GolferWaitlistEntryId);

        if (entry is null)
        {
            return Results.NotFound(new { error = "Waitlist entry not found." });
        }

        var golfer = await golferRepository.GetByIdAsync(entry.GolferId);

        if (golfer is null)
        {
            return Results.NotFound(new { error = "Golfer not found." });
        }

        offer.Accept(golfer);

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
    int GolfersNeeded,
    string GolferName,
    string Status);

public record WaitlistOfferAcceptResponse(
    string Status,
    string Message);
