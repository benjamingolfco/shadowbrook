using System.Data;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Events;

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
        var offer = await db.WaitlistOffers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Token == token);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        // Check expiration
        if (offer.Status == OfferStatus.Pending && offer.ExpiresAt < DateTimeOffset.UtcNow)
        {
            offer.Status = OfferStatus.Expired;
            await db.SaveChangesAsync();
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
        ApplicationDbContext db,
        IDomainEventPublisher eventPublisher,
        CancellationToken ct)
    {
        var offer = await db.WaitlistOffers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Token == token, ct);

        if (offer is null)
        {
            return Results.NotFound(new { error = "Offer not found." });
        }

        if (offer.Status != OfferStatus.Pending)
        {
            return Results.Conflict(new { error = "This offer is no longer available." });
        }

        if (offer.ExpiresAt < DateTimeOffset.UtcNow)
        {
            offer.Status = OfferStatus.Expired;
            await db.SaveChangesAsync(ct);
            return Results.Conflict(new { error = "This offer has expired." });
        }

        // Use serializable transaction to prevent race condition when checking slot capacity
        // Without this, two golfers could simultaneously check the count, both see available slots,
        // and both create acceptances - exceeding the requested golfer count
        using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        // Check concurrency: verify slots are still available
        var acceptanceCount = await db.WaitlistRequestAcceptances
            .Where(a => a.WaitlistRequestId == offer.TeeTimeRequestId)
            .CountAsync(ct);

        if (acceptanceCount >= offer.GolfersNeeded)
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
        offer.Status = OfferStatus.Accepted;

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

        // Publish domain event
        await eventPublisher.PublishAsync(new WaitlistOfferAccepted
        {
            WaitlistOfferId = offer.Id,
            TeeTimeRequestId = offer.TeeTimeRequestId,
            GolferWaitlistEntryId = offer.GolferWaitlistEntryId,
            CourseId = offer.CourseId,
            CourseName = offer.CourseName,
            Date = offer.Date,
            TeeTime = offer.TeeTime,
            GolferName = offer.GolferName,
            GolferPhone = offer.GolferPhone,
            GolfersNeeded = offer.GolfersNeeded,
            AcceptanceCount = acceptanceCount + 1
        }, ct);

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
