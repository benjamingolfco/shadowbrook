using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Events;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Endpoints;

public static class WaitlistEndpoints
{
    public static void MapWaitlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/courses/{courseId:guid}");

        group.MapGet("waitlist-settings", GetWaitlistSettings);
        group.MapPut("waitlist-settings", UpdateWaitlistSettings);
        group.MapGet("waitlist", GetWaitlist);
        group.MapPost("waitlist/requests", CreateWaitlistRequest);
    }

    private static async Task<IResult> GetWaitlistSettings(
        Guid courseId,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new WaitlistSettingsResponse(course.WaitlistEnabled ?? false));
    }

    private static async Task<IResult> UpdateWaitlistSettings(
        Guid courseId,
        WaitlistSettingsRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound();
        }

        course.WaitlistEnabled = request.WaitlistEnabled;
        course.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return Results.Ok(new WaitlistSettingsResponse(course.WaitlistEnabled.Value));
    }

    private static async Task<IResult> GetWaitlist(
        Guid courseId,
        string? date,
        ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(date) ||
            !DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        {
            return Results.BadRequest(new { error = "A valid date in yyyy-MM-dd format is required." });
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound();
        }

        if (course.WaitlistEnabled != true)
        {
            return Results.BadRequest(new { error = "Waitlist is not enabled for this course." });
        }

        var courseWaitlist = await db.CourseWaitlists
            .Include(cw => cw.WaitlistRequests)
            .FirstOrDefaultAsync(cw => cw.CourseId == courseId && cw.Date == parsedDate);

        if (courseWaitlist is null)
        {
            return Results.Ok(new WaitlistResponse(
                null,
                date,
                0,
                new List<WaitlistRequestResponse>()));
        }

        var requests = courseWaitlist.WaitlistRequests
            .OrderBy(wr => wr.TeeTime)
            .Select(wr => new WaitlistRequestResponse(
                wr.Id,
                wr.TeeTime.ToString("HH:mm"),
                wr.GolfersNeeded,
                wr.Status))
            .ToList();

        var totalPending = courseWaitlist.WaitlistRequests
            .Where(wr => wr.Status == "Pending")
            .Sum(wr => wr.GolfersNeeded);

        return Results.Ok(new WaitlistResponse(
            courseWaitlist.Id,
            date,
            totalPending,
            requests));
    }

    private static async Task<IResult> CreateWaitlistRequest(
        Guid courseId,
        CreateWaitlistRequestRequest request,
        ApplicationDbContext db,
        IDomainEventPublisher eventPublisher)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound();
        }

        if (course.WaitlistEnabled != true)
        {
            return Results.BadRequest(new { error = "Waitlist is not enabled for this course." });
        }

        if (string.IsNullOrWhiteSpace(request.Date) ||
            !DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", out var parsedDate))
        {
            return Results.BadRequest(new { error = "A valid date in yyyy-MM-dd format is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TeeTime) ||
            !TimeOnly.TryParseExact(request.TeeTime, new[] { "HH:mm", "HH:mm:ss" }, out var parsedTeeTime))
        {
            return Results.BadRequest(new { error = "A valid tee time in HH:mm format is required." });
        }

        if (request.GolfersNeeded is < 1 or > 4)
        {
            return Results.BadRequest(new { error = "Golfers needed must be between 1 and 4." });
        }

        // Find or create CourseWaitlist for this course and date
        var courseWaitlist = await db.CourseWaitlists
            .FirstOrDefaultAsync(cw => cw.CourseId == courseId && cw.Date == parsedDate);

        if (courseWaitlist is null)
        {
            // Generate a unique 4-digit short code for the date
            string? shortCode = null;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var candidate = Random.Shared.Next(0, 10000).ToString("D4");
                var taken = await db.CourseWaitlists
                    .AnyAsync(w => w.ShortCode == candidate && w.Date == parsedDate);
                if (!taken)
                {
                    shortCode = candidate;
                    break;
                }
            }

            if (shortCode is null)
            {
                return Results.Problem("Unable to generate a unique short code.", statusCode: 500);
            }

            var now = DateTimeOffset.UtcNow;
            courseWaitlist = new CourseWaitlist
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                Date = parsedDate,
                ShortCode = shortCode,
                Status = "Open",
                OpenedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.CourseWaitlists.Add(courseWaitlist);
        }

        // Check for existing active request for this tee time
        var duplicate = await db.WaitlistRequests
            .AnyAsync(wr =>
                wr.CourseWaitlistId == courseWaitlist.Id &&
                wr.TeeTime == parsedTeeTime &&
                wr.Status == "Pending");

        if (duplicate)
        {
            return Results.Conflict(new { error = "An active waitlist request already exists for this tee time." });
        }

        var waitlistRequest = new WaitlistRequest
        {
            Id = Guid.NewGuid(),
            CourseWaitlistId = courseWaitlist.Id,
            TeeTime = parsedTeeTime,
            GolfersNeeded = request.GolfersNeeded,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.WaitlistRequests.Add(waitlistRequest);
        await db.SaveChangesAsync();

        await eventPublisher.PublishAsync(new WaitlistRequestCreated
        {
            WaitlistRequestId = waitlistRequest.Id,
            CourseWaitlistId = courseWaitlist.Id,
            CourseId = courseId,
            Date = parsedDate,
            TeeTime = parsedTeeTime,
            GolfersNeeded = request.GolfersNeeded
        });

        var response = new WaitlistRequestResponse(
            waitlistRequest.Id,
            waitlistRequest.TeeTime.ToString("HH:mm"),
            waitlistRequest.GolfersNeeded,
            waitlistRequest.Status);

        return Results.Created($"/courses/{courseId}/waitlist/requests/{waitlistRequest.Id}", response);
    }
}

public record WaitlistSettingsRequest(bool WaitlistEnabled);
public record WaitlistSettingsResponse(bool WaitlistEnabled);
public record CreateWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);
public record WaitlistResponse(
    Guid? CourseWaitlistId,
    string Date,
    int TotalGolfersPending,
    List<WaitlistRequestResponse> Requests);
public record WaitlistRequestResponse(
    Guid Id,
    string TeeTime,
    int GolfersNeeded,
    string Status);
