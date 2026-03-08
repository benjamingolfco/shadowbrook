using FluentValidation;
using Shadowbrook.Api.Endpoints.Filters;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpWaitlistEndpoints
{
    public static void MapWalkUpWaitlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/courses/{courseId:guid}/walkup-waitlist")
            .AddEndpointFilter<CourseExistsFilter>();

        group.MapPost("/open", OpenWaitlist);
        group.MapPost("/close", CloseWaitlist);
        group.MapGet("/today", GetToday);
        group.MapPost("/requests", CreateWaitlistRequest);
    }

    private static async Task<IResult> OpenWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await WalkUpWaitlistEntity.OpenAsync(courseId, today, shortCodeGenerator, repo);

        repo.Add(waitlist);
        await repo.SaveAsync();

        var response = ToResponse(waitlist);
        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", response);
    }

    private static async Task<IResult> CloseWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo)
    {
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
        IWalkUpWaitlistRepository repo)
    {
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
        IWalkUpWaitlistRepository repo)
    {
        var parsedDate = DateOnly.ParseExact(request.Date, "yyyy-MM-dd");
        var parsedTeeTime = TimeOnly.ParseExact(request.TeeTime, ["HH:mm", "HH:mm:ss"]);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, parsedDate);

        if (waitlist is null)
        {
            return Results.BadRequest(new { error = "No open walk-up waitlist found for this date." });
        }

        var teeTimeRequest = waitlist.AddTeeTimeRequest(parsedTeeTime, request.GolfersNeeded);
        await repo.SaveAsync();

        var response = new WalkUpWaitlistRequestResponse(
            teeTimeRequest.Id,
            teeTimeRequest.TeeTime.ToString("HH:mm"),
            teeTimeRequest.GolfersNeeded,
            teeTimeRequest.Status.ToString());

        return Results.Created($"/courses/{courseId}/walkup-waitlist/requests/{teeTimeRequest.Id}", response);
    }

    private static WalkUpWaitlistResponse ToResponse(WalkUpWaitlistEntity w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status.ToString(), w.OpenedAt, w.ClosedAt);
}

public record CreateWalkUpWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);

public class CreateWalkUpWaitlistRequestRequestValidator : AbstractValidator<CreateWalkUpWaitlistRequestRequest>
{
    public CreateWalkUpWaitlistRequestRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", out _))
            .WithMessage("A valid date in yyyy-MM-dd format is required.");

        RuleFor(x => x.TeeTime)
            .NotEmpty().WithMessage("Tee time is required.")
            .Must(t => TimeOnly.TryParseExact(t, ["HH:mm", "HH:mm:ss"], out _))
            .WithMessage("A valid tee time in HH:mm format is required.");

        RuleFor(x => x.GolfersNeeded)
            .InclusiveBetween(1, 4)
            .WithMessage("Golfers needed must be between 1 and 4.");
    }
}

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
