using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Events;
using Shadowbrook.Api.Models;
using Shadowbrook.Api.Services;

namespace Shadowbrook.Api.Endpoints;

public static class WalkupJoinEndpoints
{
    public static void MapWalkupJoinEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/walkup");

        group.MapPost("/verify", VerifyShortCode);
        group.MapPost("/join", JoinWaitlist);
    }

    private static async Task<IResult> VerifyShortCode(VerifyCodeRequest request, ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 4 || !request.Code.All(char.IsDigit))
        {
            return Results.BadRequest(new { error = "Code must be exactly 4 digits." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .Include(w => w.Course)
            .FirstOrDefaultAsync(w => w.ShortCode == request.Code && w.Date == today && w.Status == "Open");

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "Code not found or waitlist is not active." });
        }

        return Results.Ok(new VerifyCodeResponse(
            waitlist.Id,
            waitlist.Course!.Name,
            waitlist.ShortCode));
    }

    private static async Task<IResult> JoinWaitlist(JoinWaitlistRequest request, ApplicationDbContext db, IDomainEventPublisher publisher)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return Results.BadRequest(new { error = "First name is required." });
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return Results.BadRequest(new { error = "Last name is required." });
        }

        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);
        if (normalizedPhone is null)
        {
            return Results.BadRequest(new { error = "A valid US phone number is required." });
        }

        var waitlist = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .Include(w => w.Course)
            .FirstOrDefaultAsync(w => w.Id == request.CourseWaitlistId);

        if (waitlist is null || waitlist.Status != "Open")
        {
            return Results.NotFound(new { error = "This waitlist is no longer accepting new entries." });
        }

        // Check for existing active entry (duplicate prevention)
        var existingEntry = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.GolferPhone == normalizedPhone && e.RemovedAt == null)
            .FirstOrDefaultAsync();

        if (existingEntry is not null)
        {
            var existingPosition = await CalculatePositionAsync(db, waitlist.Id, existingEntry.JoinedAt);
            return Results.Conflict(new { error = "You're already on the waitlist.", position = existingPosition });
        }

        // Golfer lookup-or-create
        var golfer = await db.Golfers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

        if (golfer is null)
        {
            var now = DateTimeOffset.UtcNow;
            golfer = new Golfer
            {
                Id = Guid.NewGuid(),
                Phone = normalizedPhone,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Golfers.Add(golfer);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: another request created the golfer concurrently
                db.Golfers.Remove(golfer);
                golfer = await db.Golfers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

                if (golfer is null)
                {
                    return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
                }
            }
        }
        else
        {
            // Update name if provided info differs
            var now = DateTimeOffset.UtcNow;
            golfer.FirstName = request.FirstName.Trim();
            golfer.LastName = request.LastName.Trim();
            golfer.UpdatedAt = now;
        }

        var entryNow = DateTimeOffset.UtcNow;
        var entry = new GolferWaitlistEntry
        {
            Id = Guid.NewGuid(),
            CourseWaitlistId = waitlist.Id,
            GolferId = golfer.Id,
            GolferName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            GolferPhone = normalizedPhone,
            IsWalkUp = true,
            IsReady = true,
            JoinedAt = entryNow,
            CreatedAt = entryNow,
            UpdatedAt = entryNow
        };

        db.GolferWaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var position = await CalculatePositionAsync(db, waitlist.Id, entry.JoinedAt);

        await publisher.PublishAsync(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            CourseWaitlistId = waitlist.Id,
            GolferId = golfer.Id,
            GolferName = entry.GolferName,
            GolferPhone = normalizedPhone,
            CourseName = waitlist.Course!.Name,
            Position = position
        });

        return Results.Created($"/walkup/join", new JoinWaitlistResponse(
            entry.Id,
            entry.GolferName,
            position,
            waitlist.Course!.Name));
    }

    // Position is determined by how many active entries joined at or before the given timestamp.
    // We load JoinedAt values client-side to avoid DateTimeOffset translation issues with SQLite.
    private static async Task<int> CalculatePositionAsync(ApplicationDbContext db, Guid courseWaitlistId, DateTimeOffset joinedAt)
    {
        var joinedAtValues = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == courseWaitlistId && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();

        return joinedAtValues.Count(t => t <= joinedAt);
    }
}

public record VerifyCodeRequest(string Code);

public record VerifyCodeResponse(
    Guid CourseWaitlistId,
    string CourseName,
    string ShortCode);

public record JoinWaitlistRequest(
    Guid CourseWaitlistId,
    string FirstName,
    string LastName,
    string Phone);

public record JoinWaitlistResponse(
    Guid EntryId,
    string GolferName,
    int Position,
    string CourseName);
