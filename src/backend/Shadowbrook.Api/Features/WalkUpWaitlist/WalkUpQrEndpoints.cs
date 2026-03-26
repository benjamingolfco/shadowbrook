using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.WalkUpWaitlist;

public static class WalkUpQrEndpoints
{
    [WolverineGet("/walkup/status/{shortCode}")]
    public static async Task<IResult> GetWalkUpStatus(
        string shortCode,
        ApplicationDbContext db,
        TimeProvider timeProvider)
    {
        var waitlist = await db.CourseWaitlists
            .OfType<Domain.CourseWaitlistAggregate.WalkUpWaitlist>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.ShortCode == shortCode);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "This QR code is not valid." });
        }

        // Course guaranteed to exist via FK
        var course = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => new { c.Name, c.TimeZoneId })
            .FirstAsync();

        var today = CourseTime.Today(timeProvider, course.TimeZoneId);

        var status = waitlist.Date != today ? "expired" : waitlist.Status == WaitlistStatus.Open ? "open" : "closed";
        return Results.Ok(new WalkUpQrStatusResponse(
            status,
            course.Name,
            waitlist.Date.ToString("yyyy-MM-dd")));
    }
}

public record WalkUpQrStatusResponse(
    string Status,
    string CourseName,
    string Date);
