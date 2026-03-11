using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Api.Endpoints;

public static class SmsWebhookEndpoints
{
    public static void MapSmsWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks/sms");
        group.MapPost("/inbound", HandleInboundSms);
    }

    private static async Task<IResult> HandleInboundSms(
        InboundSmsWebhookRequest request,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("SmsWebhook");

        // Validate phone — if invalid, silently return 200 (webhook must not error)
        var normalizedPhone = PhoneNormalizer.Normalize(request.From);
        if (normalizedPhone is null)
        {
            logger.LogWarning("Inbound SMS from unrecognizable phone '{From}'. Ignored.", request.From);
            return Results.Ok();
        }

        // Find the most recent pending offer for this phone number.
        // Load matching offers client-side and sort in memory — SQLite does not support
        // DateTimeOffset ordering in EF Core queries.
        var pendingOffers = await db.WaitlistOffers
            .Where(o => o.GolferPhone == normalizedPhone && o.Status == OfferStatus.Pending)
            .ToListAsync();

        var offer = pendingOffers
            .OrderByDescending(o => o.OfferedAt)
            .FirstOrDefault();

        if (offer is null)
        {
            logger.LogInformation("Inbound SMS from {Phone} with no active pending offer. Ignored.", normalizedPhone);
            return Results.Ok();
        }

        var body = request.Body.Trim().ToUpperInvariant();
        var now = timeProvider.GetUtcNow();

        if (body.StartsWith('Y'))
        {
            await ProcessClaimAsync(offer, normalizedPhone, now, db, textMessageService, logger);
        }
        else if (body.StartsWith('N'))
        {
            await ProcessDeclineAsync(offer, normalizedPhone, now, db, textMessageService, logger);
        }
        else
        {
            await textMessageService.SendAsync(normalizedPhone,
                "Reply Y to claim the tee time or N to pass.", CancellationToken.None);
        }

        return Results.Ok();
    }

    private static async Task ProcessClaimAsync(
        WaitlistOffer offer,
        string normalizedPhone,
        DateTimeOffset now,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        ILogger logger)
    {
        // Check expiry
        if (now > offer.ExpiresAt)
        {
            var teeTimeFormatted = offer.TeeTime.ToString("h:mm tt");
            await textMessageService.SendAsync(normalizedPhone,
                $"Sorry, the response window for the {teeTimeFormatted} tee time has closed.",
                CancellationToken.None);
            return;
        }

        // Check if already claimed by someone else
        var alreadyClaimed = await db.WaitlistRequestAcceptances
            .AnyAsync(a => a.WaitlistRequestId == offer.TeeTimeRequestId);

        if (alreadyClaimed)
        {
            var teeTimeFormatted = offer.TeeTime.ToString("h:mm tt");
            await textMessageService.SendAsync(normalizedPhone,
                $"Sorry, the {teeTimeFormatted} slot at {offer.CourseName} has already been taken.",
                CancellationToken.None);
            return;
        }

        // Load related entities needed to complete the claim
        var teeTimeRequest = await db.TeeTimeRequests
            .FirstAsync(r => r.Id == offer.TeeTimeRequestId);

        var waitlistEntry = await db.GolferWaitlistEntries
            .FirstAsync(e => e.Id == offer.GolferWaitlistEntryId);

        // Look up the CourseId and Date from the WalkUpWaitlist (via TeeTimeRequest.WalkUpWaitlistId)
        var waitlist = await db.WalkUpWaitlists
            .FirstAsync(w => w.Id == teeTimeRequest.WalkUpWaitlistId);

        // Transition the offer
        offer.Accept(now);

        // Record the acceptance (unique constraint prevents double-booking)
        var acceptance = new WaitlistRequestAcceptance
        {
            Id = Guid.CreateVersion7(),
            WaitlistRequestId = offer.TeeTimeRequestId,
            GolferWaitlistEntryId = offer.GolferWaitlistEntryId,
            AcceptedAt = now,
            CreatedAt = now
        };
        db.WaitlistRequestAcceptances.Add(acceptance);

        // Create the booking
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            CourseId = waitlist.CourseId,
            Date = waitlist.Date,
            Time = offer.TeeTime,
            GolferName = waitlistEntry.GolferName,
            PlayerCount = waitlistEntry.GroupSize,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Bookings.Add(booking);

        // Fulfill the tee time request
        teeTimeRequest.Fulfill();

        // Soft-delete the waitlist entry — golfer is no longer in queue
        waitlistEntry.RemovedAt = now;
        waitlistEntry.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
        {
            // Race condition: another request claimed the slot concurrently
            var teeTimeFormatted = offer.TeeTime.ToString("h:mm tt");
            await textMessageService.SendAsync(normalizedPhone,
                $"Sorry, the {teeTimeFormatted} slot at {offer.CourseName} has already been taken.",
                CancellationToken.None);
            return;
        }

        await textMessageService.SendAsync(normalizedPhone,
            $"Confirmed! You're booked for {offer.TeeTime:h:mm tt} at {offer.CourseName}. See you on the first tee!",
            CancellationToken.None);
    }

    private static async Task ProcessDeclineAsync(
        WaitlistOffer offer,
        string normalizedPhone,
        DateTimeOffset now,
        ApplicationDbContext db,
        ITextMessageService textMessageService,
        ILogger logger)
    {
        offer.Decline(now);

        // Soft-delete the waitlist entry — golfer passes on this and is removed
        var waitlistEntry = await db.GolferWaitlistEntries
            .FirstAsync(e => e.Id == offer.GolferWaitlistEntryId);

        waitlistEntry.RemovedAt = now;
        waitlistEntry.UpdatedAt = now;

        await db.SaveChangesAsync();

        await textMessageService.SendAsync(normalizedPhone,
            $"Got it - you've been removed from the waitlist at {offer.CourseName}.",
            CancellationToken.None);
    }

    private static bool IsDuplicateKeyViolation(DbUpdateException ex)
    {
        // SQLite: "UNIQUE constraint failed"
        // SQL Server: error 2601 or 2627
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unique", StringComparison.OrdinalIgnoreCase)
               || message.Contains("2601")
               || message.Contains("2627");
    }
}

public record InboundSmsWebhookRequest(string From, string Body);

public class InboundSmsWebhookRequestValidator : AbstractValidator<InboundSmsWebhookRequest>
{
    public InboundSmsWebhookRequestValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From phone number is required.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Message body is required.");
    }
}
