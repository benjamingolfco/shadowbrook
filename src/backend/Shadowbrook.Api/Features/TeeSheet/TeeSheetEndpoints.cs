using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.TeeSheet;

public static class TeeSheetEndpoints
{
    [WolverineGet("/tee-sheets")]
    public static async Task<IResult> GetTeeSheet(
        Guid? courseId,
        string? date,
        ApplicationDbContext db)
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

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId.Value);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
        {
            return Results.NotFound(new { error = "Tee time settings not configured for this course." });
        }

        // Fetch all bookings for the course and date
        var dayStart = dateOnly.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dateOnly.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var bookings = await db.Bookings
            .Where(b => b.CourseId == courseId.Value && b.TeeTime.Value >= dayStart && b.TeeTime.Value < dayEnd)
            .ToListAsync();

        // Generate all time slots
        var slots = new List<TeeSheetSlot>();
        var currentTime = course.FirstTeeTime.Value;
        var interval = TimeSpan.FromMinutes(course.TeeTimeIntervalMinutes.Value);

        while (currentTime < course.LastTeeTime.Value)
        {
            var booking = bookings.FirstOrDefault(b => b.TeeTime.Time == currentTime);

            slots.Add(new TeeSheetSlot(
                currentTime.ToString("HH:mm"),
                booking is not null ? "booked" : "open",
                booking?.GolferName,
                booking?.PlayerCount ?? 0));

            currentTime = currentTime.Add(interval);
        }

        return Results.Ok(new TeeSheetResponse(
            course.Id,
            course.Name,
            dateOnly.ToString("yyyy-MM-dd"),
            slots));
    }
}

public record TeeSheetResponse(
    Guid CourseId,
    string CourseName,
    string Date,
    List<TeeSheetSlot> Slots);

public record TeeSheetSlot(
    string Time,
    string Status,
    string? GolferName,
    int PlayerCount);
