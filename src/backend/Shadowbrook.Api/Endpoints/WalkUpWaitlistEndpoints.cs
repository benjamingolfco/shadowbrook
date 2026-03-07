using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

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

    private static async Task<IResult> OpenWaitlist(
        Guid courseId,
        ApplicationDbContext db,
        IWalkUpWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await repo.GetByCourseDateAsync(courseId, today);

        if (existing is not null)
        {
            if (existing.Status == WaitlistStatus.Open)
            {
                return Results.Conflict(new { error = "Walk-up waitlist is already open for today." });
            }

            return Results.Conflict(new { error = "Walk-up waitlist was already used today." });
        }

        var waitlist = await WalkUpWaitlistEntity.OpenAsync(courseId, today, shortCodeGenerator);

        repo.Add(waitlist);
        await repo.SaveAsync();

        var response = ToResponse(waitlist);
        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", response);
    }

    private static async Task<IResult> CloseWaitlist(
        Guid courseId,
        ApplicationDbContext db,
        IWalkUpWaitlistRepository repo)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        waitlist.Close();

        await repo.SaveAsync();

        return Results.Ok(ToResponse(waitlist));
    }

    private static async Task<IResult> GetToday(
        Guid courseId,
        ApplicationDbContext db,
        IWalkUpWaitlistRepository repo)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

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
        IWalkUpWaitlistRepository repo)
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

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, parsedDate);

        if (waitlist is null)
        {
            return Results.BadRequest(new { error = "No open walk-up waitlist found for this date." });
        }

        try
        {
            var teeTimeRequest = waitlist.AddTeeTimeRequest(parsedTeeTime, request.GolfersNeeded);
            await repo.SaveAsync();

            var response = new WalkUpWaitlistRequestResponse(
                teeTimeRequest.Id,
                teeTimeRequest.TeeTime.ToString("HH:mm"),
                teeTimeRequest.GolfersNeeded,
                teeTimeRequest.Status.ToString());

            return Results.Created($"/courses/{courseId}/walkup-waitlist/requests/{teeTimeRequest.Id}", response);
        }
        catch (DuplicateTeeTimeRequestException)
        {
            return Results.Conflict(new { error = "An active waitlist request already exists for this tee time." });
        }
    }

    private static WalkUpWaitlistResponse ToResponse(WalkUpWaitlistEntity w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status.ToString(), w.OpenedAt, w.ClosedAt);
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
