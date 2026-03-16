using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
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

    private static async Task<IResult> GetOffer(Guid token, ApplicationDbContext db)
    {
        var offer = await db.WaitlistOffers.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Token == token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        var request = await db.TeeTimeRequests.FindAsync(offer.TeeTimeRequestId);
        if (request is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        var entry = await db.GolferWaitlistEntries.FindAsync(offer.GolferWaitlistEntryId);
        if (entry is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        var golfer = await db.Golfers.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == entry.GolferId);
        if (golfer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        var course = await db.Courses.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == request.CourseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        return Results.Ok(new WaitlistOfferResponse(
            offer.Token,
            course.Name,
            request.Date.ToString("yyyy-MM-dd"),
            request.TeeTime.ToString("HH:mm"),
            request.GolfersNeeded,
            golfer.FullName,
            offer.Status.ToString()));
    }

    private static async Task<IResult> AcceptOffer(
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
        await offerRepository.SaveAsync();

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
