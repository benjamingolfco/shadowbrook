using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Events;
using Shadowbrook.Api.Models;
using Shadowbrook.Api.Services;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpEndpoints
{
    /// <summary>
    /// Rate limit policy name for the verify endpoint.
    /// Defined here so the policy registration in Program.cs and the endpoint stay in sync.
    /// </summary>
    public const string RateLimitPolicyName = "walkup-verify";

    public static void MapWalkUpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/walkup");

        // Public endpoints — no tenant header required, no auth
        // /verify is rate-limited per IP to prevent 4-digit code enumeration (10,000 combinations)
        group.MapPost("verify", VerifyCode).RequireRateLimiting(RateLimitPolicyName);
        group.MapPost("join", JoinWaitlist);
    }

    /// <summary>
    /// Validate a 4-digit walk-up code and return the course name and waitlist ID.
    /// Creates the CourseWaitlist for today if it does not yet exist.
    /// </summary>
    private static async Task<IResult> VerifyCode(
        VerifyCodeRequest request,
        ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 4 || !request.Code.All(char.IsDigit))
            return Results.BadRequest(new { error = "Code must be exactly 4 numeric digits." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Look for today's code
        var walkUpCode = await db.WalkUpCodes
            .Include(w => w.Course)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Code == request.Code && w.Date == today);

        if (walkUpCode is null)
        {
            // Check if the code exists for a past date (expired vs unknown)
            var existsForPastDate = await db.WalkUpCodes
                .AnyAsync(w => w.Code == request.Code && w.Date < today);

            return existsForPastDate
                ? Results.Json(new { error = "This waitlist code has expired. Ask the pro shop for today's code." }, statusCode: 410)
                : Results.NotFound(new { error = "Code not found. Check the code posted at the course and try again." });
        }

        if (!walkUpCode.IsActive)
            return Results.Json(new { error = "This waitlist code has expired. Ask the pro shop for today's code." }, statusCode: 410);

        var course = walkUpCode.Course!;

        // Find or create today's CourseWaitlist
        var waitlist = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.CourseId == course.Id && w.Date == today);

        if (waitlist is null)
        {
            waitlist = new CourseWaitlist
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                Date = today,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.CourseWaitlists.Add(waitlist);
            await db.SaveChangesAsync();
        }

        return Results.Ok(new VerifyCodeResponse(
            waitlist.Id,
            course.Id,
            course.Name,
            today.ToString("yyyy-MM-dd")));
    }

    /// <summary>
    /// Add a golfer to the walk-up waitlist.
    /// Uses find-or-create for both Golfer (by phone) and handles duplicates gracefully.
    /// </summary>
    private static async Task<IResult> JoinWaitlist(
        JoinWaitlistRequest request,
        ApplicationDbContext db,
        IDomainEventPublisher eventPublisher)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return Results.BadRequest(new { error = "First name is required." });

        if (string.IsNullOrWhiteSpace(request.LastName))
            return Results.BadRequest(new { error = "Last name is required." });

        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);
        if (normalizedPhone is null)
            return Results.BadRequest(new { error = "Please enter a valid US phone number." });

        // Load the CourseWaitlist — must exist (created during verify step)
        var waitlist = await db.CourseWaitlists
            .Include(w => w.Course)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == request.CourseWaitlistId);

        if (waitlist is null)
            return Results.NotFound(new { error = "Waitlist not found." });

        var courseName = waitlist.Course!.Name;

        // Find or create Golfer by normalized phone
        var golfer = await db.Golfers.FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

        if (golfer is null)
        {
            golfer = new Golfer
            {
                Id = Guid.NewGuid(),
                Phone = normalizedPhone,
                FirstName = request.FirstName,
                LastName = request.LastName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Golfers.Add(golfer);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique constraint violation on Phone — another request created the Golfer
                // concurrently. Re-query to get the existing record.
                db.ChangeTracker.Clear();
                golfer = await db.Golfers.FirstOrDefaultAsync(g => g.Phone == normalizedPhone);
                if (golfer is null)
                    return Results.Problem("Failed to create or retrieve golfer record.");
            }
        }
        else
        {
            // Update name if changed — golfer may re-join with a corrected name
            if (golfer.FirstName != request.FirstName || golfer.LastName != request.LastName)
            {
                golfer.FirstName = request.FirstName;
                golfer.LastName = request.LastName;
                golfer.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        // Check for existing active entry (duplicate detection)
        var existingEntry = await db.GolferWaitlistEntries
            .FirstOrDefaultAsync(e =>
                e.CourseWaitlistId == request.CourseWaitlistId &&
                e.GolferId == golfer.Id &&
                e.RemovedAt == null);

        if (existingEntry is not null)
        {
            var existingPosition = await CalculatePositionAsync(db, request.CourseWaitlistId, existingEntry.JoinedAt);

            return Results.Ok(new JoinWaitlistResponse(
                existingEntry.Id,
                golfer.FirstName,
                existingPosition,
                IsExisting: true));
        }

        // Create the new waitlist entry
        var now = DateTimeOffset.UtcNow;
        var entry = new GolferWaitlistEntry
        {
            Id = Guid.NewGuid(),
            CourseWaitlistId = request.CourseWaitlistId,
            GolferId = golfer.Id,
            GolferName = $"{request.FirstName} {request.LastName}",
            GolferPhone = normalizedPhone,
            IsWalkUp = true,
            IsReady = true,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.GolferWaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        // Calculate position (1-based: count of active entries joined at or before this one)
        var position = await CalculatePositionAsync(db, request.CourseWaitlistId, entry.JoinedAt);

        // Publish domain event — SMS confirmation fires after the data is safely persisted
        await eventPublisher.PublishAsync(new GolferJoinedWaitlist
        {
            GolferWaitlistEntryId = entry.Id,
            GolferId = golfer.Id,
            GolferPhone = normalizedPhone,
            GolferFirstName = golfer.FirstName,
            CourseName = courseName,
            Position = position
        });

        return Results.Ok(new JoinWaitlistResponse(
            entry.Id,
            golfer.FirstName,
            position,
            IsExisting: false));
    }

    /// <summary>
    /// Calculates a golfer's 1-based position on the waitlist.
    /// Fetches active entries ordered by JoinedAt and counts those joined at or before the target time.
    /// This approach works reliably across both SQLite (tests) and SQL Server (production),
    /// avoiding DateTimeOffset comparison translation issues in EF Core/SQLite.
    /// </summary>
    private static async Task<int> CalculatePositionAsync(
        ApplicationDbContext db,
        Guid courseWaitlistId,
        DateTimeOffset joinedAt)
    {
        var joinedAtTicks = joinedAt.UtcTicks;

        var activeEntries = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == courseWaitlistId && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();

        return activeEntries.Count(t => t.UtcTicks <= joinedAtTicks);
    }
}

public record VerifyCodeRequest(string Code);

public record VerifyCodeResponse(
    Guid CourseWaitlistId,
    Guid CourseId,
    string CourseName,
    string Date);

public record JoinWaitlistRequest(
    Guid CourseWaitlistId,
    string FirstName,
    string LastName,
    string Phone);

public record JoinWaitlistResponse(
    Guid EntryId,
    string FirstName,
    int Position,
    bool IsExisting);
