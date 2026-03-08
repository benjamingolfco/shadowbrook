using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Endpoints.Filters;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpJoinEndpoints
{
    public static void MapWalkUpJoinEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/walkup")
            .AddValidationFilter();

        group.MapPost("/verify", VerifyShortCode);
        group.MapPost("/join", JoinWaitlist);
    }

    private static async Task<IResult> VerifyShortCode(VerifyCodeRequest request, ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.ShortCode == request.Code && w.Date == today && w.Status == WaitlistStatus.Open);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "Code not found or waitlist is not active." });
        }

        // Course guaranteed to exist via FK
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => c.Name)
            .FirstAsync();

        return Results.Ok(new VerifyCodeResponse(
            waitlist.Id,
            courseName,
            waitlist.ShortCode));
    }

    private static async Task<IResult> JoinWaitlist(JoinWaitlistRequest request, ApplicationDbContext db)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == request.CourseWaitlistId);

        if (waitlist is null || waitlist.Status != WaitlistStatus.Open)
        {
            return Results.NotFound(new { error = "This waitlist is no longer accepting new entries." });
        }

        // Course guaranteed to exist via FK
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => c.Name)
            .FirstAsync();

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
                Id = Guid.CreateVersion7(),
                Phone = normalizedPhone!,
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
        // Existing golfer: name was set at registration and is not overwritten here

        var entryNow = DateTimeOffset.UtcNow;
        var entry = new GolferWaitlistEntry(Guid.CreateVersion7())
        {
            CourseWaitlistId = waitlist.Id,
            GolferId = golfer.Id,
            GolferName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            GolferPhone = normalizedPhone!,
            IsWalkUp = true,
            IsReady = true,
            JoinedAt = entryNow,
            CreatedAt = entryNow,
            UpdatedAt = entryNow
        };

        db.GolferWaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var position = await CalculatePositionAsync(db, waitlist.Id, entry.JoinedAt);

        entry.RaiseJoinedEvent(courseName, position);
        await db.SaveChangesAsync();

        return Results.Created($"/walkup/join", new JoinWaitlistResponse(
            entry.Id,
            entry.GolferName,
            position,
            courseName));
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

public class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Length(4).WithMessage("Code must be exactly 4 digits.")
            .Must(c => c.All(char.IsDigit)).WithMessage("Code must be exactly 4 digits.");
    }
}

public record VerifyCodeResponse(
    Guid CourseWaitlistId,
    string CourseName,
    string ShortCode);

public record JoinWaitlistRequest(
    Guid CourseWaitlistId,
    string FirstName,
    string LastName,
    string Phone);

public class JoinWaitlistRequestValidator : AbstractValidator<JoinWaitlistRequest>
{
    public JoinWaitlistRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.Phone)
            .Must(PhoneNormalizer.IsValid).WithMessage("A valid US phone number is required.");
    }
}

public record JoinWaitlistResponse(
    Guid EntryId,
    string GolferName,
    int Position,
    string CourseName);
