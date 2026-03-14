using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Endpoints.Filters;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

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
        group.MapPost("/entries", AddGolferToWaitlist);
    }

    private static async Task<IResult> OpenWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await WalkUpWaitlist.OpenAsync(courseId, today, shortCodeGenerator, repo);

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
        IWalkUpWaitlistRepository repo,
        ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

        var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;

        var entries = waitlist is not null
            ? (await db.GolferWaitlistEntries
                .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
                .Join(db.Golfers.IgnoreQueryFilters(),
                    e => e.GolferId, g => g.Id,
                    (e, g) => new WalkUpWaitlistEntryResponse(e.Id, g.FirstName + " " + g.LastName, e.GroupSize, e.JoinedAt))
                .ToListAsync())
                .OrderBy(e => e.JoinedAt)
                .ToList()
            : new List<WalkUpWaitlistEntryResponse>();

        var requests = (await db.TeeTimeRequests
                .Where(r => r.CourseId == courseId && r.Date == today)
                .Select(r => new { r.Id, r.TeeTime, r.GolfersNeeded, r.Status })
                .ToListAsync())
                .Select(r => new WalkUpWaitlistRequestResponse(r.Id, r.TeeTime.ToString("HH:mm"), r.GolfersNeeded, r.Status.ToString()))
                .ToList();

        return Results.Ok(new WalkUpWaitlistTodayResponse(waitlistResponse, entries, requests));
    }

    private static async Task<IResult> CreateWaitlistRequest(
        Guid courseId,
        CreateWalkUpWaitlistRequestRequest request,
        TeeTimeRequestService teeTimeRequestService,
        ITeeTimeRequestRepository teeTimeRequestRepo)
    {
        var parsedDate = DateOnly.ParseExact(request.Date, "yyyy-MM-dd");
        var parsedTeeTime = TimeOnly.ParseExact(request.TeeTime, ["HH:mm", "HH:mm:ss"]);

        var teeTimeRequest = await teeTimeRequestService.CreateAsync(
            courseId, parsedDate, parsedTeeTime, request.GolfersNeeded);

        teeTimeRequestRepo.Add(teeTimeRequest);
        await teeTimeRequestRepo.SaveAsync();

        var response = new WalkUpWaitlistRequestResponse(
            teeTimeRequest.Id,
            teeTimeRequest.TeeTime.ToString("HH:mm"),
            teeTimeRequest.GolfersNeeded,
            teeTimeRequest.Status.ToString());

        return Results.Created($"/courses/{courseId}/walkup-waitlist/requests/{teeTimeRequest.Id}", response);
    }

    private static async Task<IResult> AddGolferToWaitlist(
        Guid courseId,
        AddGolferToWaitlistRequest request,
        IWalkUpWaitlistRepository repo,
        IGolferRepository golferRepo,
        ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        var courseName = await db.Courses
            .Where(c => c.Id == courseId)
            .Select(c => c.Name)
            .FirstAsync();

        // Golfer lookup-or-create
        var golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

        if (golfer is null)
        {
            golfer = Golfer.Create(normalizedPhone!, request.FirstName, request.LastName);
            golferRepo.Add(golfer);

            try
            {
                await golferRepo.SaveAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: another request created the golfer concurrently
                db.Entry(golfer).State = EntityState.Detached;
                golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

                if (golfer is null)
                {
                    return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
                }
            }
        }

        var entry = waitlist.Join(golfer, request.GroupSize ?? 1);
        await repo.SaveAsync();

        var position = waitlist.Entries.Count(e => e.RemovedAt is null && e.JoinedAt <= entry.JoinedAt);

        return Results.Created(
            $"/courses/{courseId}/walkup-waitlist/entries/{entry.Id}",
            new AddGolferToWaitlistResponse(
                entry.Id,
                golfer.FullName,
                golfer.Phone,
                entry.GroupSize,
                position,
                courseName));
    }

    private static WalkUpWaitlistResponse ToResponse(WalkUpWaitlist w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status.ToString(), w.OpenedAt, w.ClosedAt);
}

public record AddGolferToWaitlistRequest(string FirstName, string LastName, string Phone, int? GroupSize);

public class AddGolferToWaitlistRequestValidator : AbstractValidator<AddGolferToWaitlistRequest>
{
    public AddGolferToWaitlistRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.Phone)
            .Must(PhoneNormalizer.IsValid).WithMessage("A valid US phone number is required.");

        RuleFor(x => x.GroupSize)
            .InclusiveBetween(1, 4).WithMessage("Group size must be between 1 and 4.")
            .When(x => x.GroupSize.HasValue);
    }
}

public record AddGolferToWaitlistResponse(
    Guid EntryId,
    string GolferName,
    string GolferPhone,
    int GroupSize,
    int Position,
    string CourseName);

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
    List<WalkUpWaitlistEntryResponse> Entries,
    List<WalkUpWaitlistRequestResponse> Requests);

public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);
