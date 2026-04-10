using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public record WeeklyStatusRequest(string StartDate);

public class WeeklyStatusRequestValidator : AbstractValidator<WeeklyStatusRequest>
{
    public WeeklyStatusRequestValidator()
    {
        RuleFor(r => r.StartDate).NotEmpty();
    }
}

public record DayStatusResponse(string Date, string Status, Guid? TeeSheetId, int? IntervalCount);

public record WeeklyStatusResponse(string WeekStart, string WeekEnd, List<DayStatusResponse> Days);

public static class WeeklyStatusEndpoint
{
    [WolverineGet("/courses/{courseId}/tee-sheets/week")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        string? startDate,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(startDate))
        {
            return Results.BadRequest(new { error = "startDate query parameter is required." });
        }

        if (!DateOnly.TryParseExact(startDate, "yyyy-MM-dd", out var start))
        {
            return Results.BadRequest(new { error = "startDate must be in yyyy-MM-dd format." });
        }

        var end = start.AddDays(6);

        var sheets = await db.TeeSheets
            .Where(s => s.CourseId == courseId && s.Date >= start && s.Date <= end)
            .Select(s => new
            {
                s.Id,
                s.Date,
                s.Status,
                IntervalCount = s.Intervals.Count,
            })
            .ToListAsync(ct);

        var sheetsByDate = sheets.ToDictionary(s => s.Date);

        var days = new List<DayStatusResponse>();
        for (var i = 0; i < 7; i++)
        {
            var date = start.AddDays(i);
            if (sheetsByDate.TryGetValue(date, out var sheet))
            {
                days.Add(new DayStatusResponse(
                    date.ToString("yyyy-MM-dd"),
                    sheet.Status.ToString().ToLowerInvariant(),
                    sheet.Id,
                    sheet.IntervalCount));
            }
            else
            {
                days.Add(new DayStatusResponse(
                    date.ToString("yyyy-MM-dd"),
                    "notStarted",
                    null,
                    null));
            }
        }

        return Results.Ok(new WeeklyStatusResponse(
            start.ToString("yyyy-MM-dd"),
            end.ToString("yyyy-MM-dd"),
            days));
    }
}
