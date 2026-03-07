using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpWaitlistEndpoints
{
    public static void MapWalkUpWaitlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/courses/{courseId:guid}/walkup-waitlist");

        group.MapPost("/open", OpenWaitlist);
        group.MapPost("/close", CloseWaitlist);
        group.MapGet("/today", GetToday);
        group.MapPost("/requests", CreateWaitlistRequest);
    }

    private static async Task<IResult> OpenWaitlist(Guid courseId, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await db.CourseWaitlists
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == today);

        if (existing is not null)
        {
            if (existing.Status == "Open")
            {
                return Results.Conflict(new { error = "Walk-up waitlist is already open for today." });
            }

            return Results.Conflict(new { error = "Walk-up waitlist was already used today." });
        }

        // Generate a globally unique 4-digit short code for today
        string? shortCode = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = Random.Shared.Next(0, 10000).ToString("D4");
            var taken = await db.CourseWaitlists
                .AnyAsync(w => w.ShortCode == candidate && w.Date == today);
            if (!taken)
            {
                shortCode = candidate;
                break;
            }
        }

        if (shortCode is null)
        {
            return Results.Problem("Unable to generate a unique short code. Please try again.", statusCode: 500);
        }

        var now = DateTimeOffset.UtcNow;
        var waitlist = new CourseWaitlist
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Date = today,
            ShortCode = shortCode,
            Status = "Open",
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.CourseWaitlists.Add(waitlist);
        await db.SaveChangesAsync();

        var response = ToResponse(waitlist);
        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", response);
    }

    private static async Task<IResult> CloseWaitlist(Guid courseId, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await db.CourseWaitlists
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == today && w.Status == "Open");

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        var now = DateTimeOffset.UtcNow;
        waitlist.Status = "Closed";
        waitlist.ClosedAt = now;
        waitlist.UpdatedAt = now;

        await db.SaveChangesAsync();

        return Results.Ok(ToResponse(waitlist));
    }

    private static async Task<IResult> GetToday(Guid courseId, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await db.CourseWaitlists
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == today);

        var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;
        var todayResponse = new WalkUpWaitlistTodayResponse(
            waitlistResponse,
            new List<WalkUpWaitlistEntryResponse>());

        return Results.Ok(todayResponse);
    }

    private static async Task<IResult> CreateWaitlistRequest(
        Guid courseId,
        CreateWalkUpWaitlistRequestRequest request,
        ApplicationDbContext db,
        IDomainEventPublisher eventPublisher)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound();
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

        // Find today's open walk-up waitlist
        var courseWaitlist = await db.CourseWaitlists
            .FirstOrDefaultAsync(cw => cw.CourseId == courseId && cw.Date == parsedDate && cw.Status == "Open");

        if (courseWaitlist is null)
        {
            return Results.BadRequest(new { error = "No open walk-up waitlist found for this date." });
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

        var response = new WalkUpWaitlistRequestResponse(
            waitlistRequest.Id,
            waitlistRequest.TeeTime.ToString("HH:mm"),
            waitlistRequest.GolfersNeeded,
            waitlistRequest.Status);

        return Results.Created($"/courses/{courseId}/walkup-waitlist/requests/{waitlistRequest.Id}", response);
    }

    private static WalkUpWaitlistResponse ToResponse(CourseWaitlist w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status, w.OpenedAt, w.ClosedAt);
}

public record CreateWalkUpWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);

public record WalkUpWaitlistRequestResponse(
    Guid Id,
    string TeeTime,
    int GolfersNeeded,
    string Status);

public record WalkUpWaitlistResponse(
    Guid Id,
    Guid CourseId,
    string ShortCode,
    string Date,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record WalkUpWaitlistTodayResponse(
    WalkUpWaitlistResponse? Waitlist,
    List<WalkUpWaitlistEntryResponse> Entries);

public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    DateTimeOffset JoinedAt);
