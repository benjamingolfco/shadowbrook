using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
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

    private static WalkUpWaitlistResponse ToResponse(CourseWaitlist w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status, w.OpenedAt, w.ClosedAt);
}

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
