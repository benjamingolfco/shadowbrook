using System.Data;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

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

        // Check expiration and persist if status changed
        if (offer.CheckExpiration())
        {
            await repository.SaveAsync();
        }

        return Results.Ok(new WaitlistOfferResponse(
            offer.Token,
            offer.CourseName,
            offer.Date.ToString("yyyy-MM-dd"),
            offer.TeeTime.ToString("HH:mm"),
            offer.GolfersNeeded,
            offer.GolferName,
            offer.Status.ToString(),
            offer.ExpiresAt));
    }

    private static async Task<IResult> AcceptOffer(
        Guid token,
        IWaitlistOfferRepository repository,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var offer = await repository.GetByTokenAsync(token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        // Use serializable transaction to prevent race condition when checking slot capacity
        using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var acceptanceCount = await repository.GetAcceptanceCountAsync(offer.TeeTimeRequestId);

        try
        {
            offer.Accept(acceptanceCount);
        }
        catch (OfferNotPendingException)
        {
            return Results.Conflict(new { error = "This offer is no longer available." });
        }
        catch (OfferExpiredException)
        {
            await db.SaveChangesAsync(ct);
            return Results.Conflict(new { error = "This offer has expired." });
        }
        catch (OfferSlotsFilledException)
        {
            return Results.Conflict(new { error = "All slots have been filled." });
        }

        // Create acceptance record
        var now = DateTimeOffset.UtcNow;
        var acceptance = new WaitlistRequestAcceptance
        {
            Id = Guid.CreateVersion7(),
            WaitlistRequestId = offer.TeeTimeRequestId,
            GolferWaitlistEntryId = offer.GolferWaitlistEntryId,
            WaitlistOfferId = offer.Id,
            AcceptedAt = now,
            CreatedAt = now
        };

        db.WaitlistRequestAcceptances.Add(acceptance);

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation - golfer already claimed this slot
            return Results.Conflict(new { error = "You have already claimed this slot." });
        }

        return Results.Ok(new WaitlistOfferAcceptResponse(
            "Accepted",
            offer.CourseName,
            offer.Date.ToString("yyyy-MM-dd"),
            offer.TeeTime.ToString("HH:mm"),
            offer.GolferName,
            "You're booked!"));
    }
}

public record WaitlistOfferResponse(
    Guid Token,
    string CourseName,
    string Date,
    string TeeTime,
    int GolfersNeeded,
    string GolferName,
    string Status,
    DateTimeOffset ExpiresAt);

public record WaitlistOfferAcceptResponse(
    string Status,
    string CourseName,
    string Date,
    string TeeTime,
    string GolferName,
    string Message);
