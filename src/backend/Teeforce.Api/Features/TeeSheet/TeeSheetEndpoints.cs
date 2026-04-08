using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet;

public static class TeeSheetEndpoints
{
    [WolverineGet("/tee-sheets")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetTeeSheet(
        Guid? courseId,
        string? date,
        ApplicationDbContext db,
        ITeeSheetRepository teeSheetRepository,
        ITeeTimeRepository teeTimeRepository,
        CancellationToken ct)
    {
        if (courseId is null)
        {
            return Results.BadRequest(new { error = "courseId query parameter is required." });
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            return Results.BadRequest(new { error = "date query parameter is required." });
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var dateOnly))
        {
            return Results.BadRequest(new { error = "date must be in yyyy-MM-dd format." });
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId.Value, ct);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId.Value, dateOnly, ct);
        if (sheet is null)
        {
            // No sheet drafted/published yet for this date — return empty slot list
            // so the UI can render a "not yet published" state without blowing up.
            return Results.Ok(new TeeSheetResponse(course.Id, course.Name, []));
        }

        var teeTimes = await teeTimeRepository.GetByTeeSheetIdAsync(sheet.Id, ct);
        var teeTimesByInterval = teeTimes.ToDictionary(t => t.TeeSheetIntervalId);

        var golferIds = teeTimes
            .SelectMany(t => t.Claims)
            .Select(c => c.GolferId)
            .Distinct()
            .ToList();

        var golferNames = await db.Golfers
            .Where(g => golferIds.Contains(g.Id))
            .Select(g => new { g.Id, Name = g.FirstName + " " + g.LastName })
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var slots = new List<TeeSheetSlot>();
        foreach (var interval in sheet.Intervals.OrderBy(i => i.Time))
        {
            var slotDateTime = dateOnly.ToDateTime(interval.Time);

            if (!teeTimesByInterval.TryGetValue(interval.Id, out var teeTime))
            {
                slots.Add(new TeeSheetSlot(slotDateTime, "open", null, 0));
                continue;
            }

            if (teeTime.Status == TeeTimeStatus.Blocked)
            {
                slots.Add(new TeeSheetSlot(slotDateTime, "blocked", null, 0));
                continue;
            }

            var claim = teeTime.Claims.FirstOrDefault();
            if (claim is null)
            {
                slots.Add(new TeeSheetSlot(slotDateTime, "open", null, 0));
                continue;
            }

            var golferName = golferNames.TryGetValue(claim.GolferId, out var name) ? name : null;
            slots.Add(new TeeSheetSlot(slotDateTime, "booked", golferName, claim.GroupSize));
        }

        return Results.Ok(new TeeSheetResponse(course.Id, course.Name, slots));
    }
}

public record TeeSheetResponse(
    Guid CourseId,
    string CourseName,
    List<TeeSheetSlot> Slots);

public record TeeSheetSlot(
    DateTime TeeTime,
    string Status,
    string? GolferName,
    int PlayerCount);
